using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LumenWave.Demo.Editor
{
    [InitializeOnLoad]
    public static class ChordDemoBuilder
    {
        public const string Root = "Assets/_LumenWave/Demo";
        public const string TowerPath = Root + "/Scenes/Level01_ChordTower_Demo.unity";
        public const string LabPath = Root + "/Scenes/LightPrototype_Chords_Demo.unity";
        const string Cainos = "Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/PF Village Props - ";
        const string Prototype = "Assets/Cainos/Pixel Art Platformer - Village Props/Scene/Light_prototype.unity";
        static Sprite pixel;
        static Material unlit;
        static TMP_FontAsset font;

        static ChordDemoBuilder()
        {
            if (SessionState.GetBool("LumenWave.BuildPending",false)) EditorApplication.update += BuildWhenReady;
        }
        public static void BuildBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch entry point only.");
            SessionState.SetBool("LumenWave.BuildPending",true);
            PrepareText();
            EditorApplication.update -= BuildWhenReady;
            EditorApplication.update += BuildWhenReady;
        }
        static void BuildWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset") == null) return;
            SessionState.SetBool("LumenWave.BuildPending",false);
            EditorApplication.update -= BuildWhenReady;
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void PrepareText()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset") == null)
                TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        public static void BuildWindowsBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch entry point only.");
            Directory.CreateDirectory("Builds/ChordTowerDemo");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { TowerPath }, locationPathName = "Builds/ChordTowerDemo/LumenWave.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows demo build failed: " + result.summary.result);
            Debug.Log("PASS: Windows demo build / " + result.summary.totalSize + " bytes");
        }

        [MenuItem("Tools/LumenWave/Build Chord Tower Demo")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new InvalidOperationException("Save your untitled scene before building the demo.");
            foreach (var folder in new[] { "/Scenes", "/Art", "/Data" }) Directory.CreateDirectory(Root + folder);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) { PrepareText(); font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"); }
            if (font == null) throw new InvalidOperationException("TMP essentials are importing. Run Build Chord Tower Demo again after import completes.");
            pixel = CreatePixel();
            if (pixel == null) throw new InvalidOperationException("Demo pixel sprite import failed.");
            unlit = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/DemoUnlit.mat");
            if (unlit == null)
            {
                unlit = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(unlit, Root + "/Art/DemoUnlit.mat");
            }
            unlit.mainTexture = pixel.texture;
            EditorUtility.SetDirty(unlit);
            BuildScene(false);
            BuildScene(true);
            AssetDatabase.SaveAssets();
            Debug.Log("Chord demo scenes built. Unity version and package manifest were not changed.");
        }
        static string DetectPipeline()
        {
            var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            return pipeline == null ? "BuiltIn" : pipeline.GetType().FullName;
        }
        static void BuildScene(bool lab)
        {
            string path = lab ? LabPath : TowerPath;
            if (File.Exists(path)) { Debug.Log("Preserving existing demo scene: " + path); return; }
            Scene previous = SceneManager.GetActiveScene();
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Level 1.unity", path))
                throw new InvalidOperationException("Could not copy Level 1 into the new demo scene.");
            Scene scene = EditorSceneManager.OpenScene(path, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            // Work on the new copy; the source Level 1 scene is never saved over.
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            try
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0, 3.2f, -10);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5.625f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .051f, .09f);
                camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = false;
                if (DetectPipeline().Contains("Universal"))
                {
                    var pp = cameraObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                    pp.assetsPPU = 32; pp.refResolutionX = 640; pp.refResolutionY = 360;
                    pp.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
                    pp.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox;
                    var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                    data.antialiasing = AntialiasingMode.None; data.renderPostProcessing = false;
                    var ambient = new GameObject("GlobalLight2D").AddComponent<Light2D>();
                    ambient.lightType = Light2D.LightType.Global; ambient.intensity = .8f;
                }
                BuildBackdrop(cameraObject.transform, lab ? 9 : 23);
                var environment = new GameObject("Cainos Watchtower").transform;
                Platform("Entrance", -4.2f, 0, 5.2f, environment);
                var landings = lab ? new Vector3[] { new Vector3(0, 0, 5), new Vector3(4.7f, 0, 4.4f) }
                    : new Vector3[] { new Vector3(-1.5f,1.4f,3), new Vector3(1.8f,2.8f,3), new Vector3(4.3f,4.2f,2.8f),
                    new Vector3(1.2f,5.6f,3), new Vector3(-2,7,3), new Vector3(-4.2f,8.4f,2.8f),
                    new Vector3(-1.1f,9.8f,3), new Vector3(2.1f,11.2f,3), new Vector3(4.3f,12.6f,2.8f),
                    new Vector3(.9f,14,3.3f), new Vector3(-2.5f,15.4f,3.4f) };
                for (int i = 0; i < landings.Length; i++) Platform("Landing " + (i + 1), landings[i].x, landings[i].y, landings[i].z, environment);
                Props(environment, lab);
                Vector2 spawn = new Vector2(-4.8f, .55f);
                var player = CreatePlayer(spawn);
                var input = player.GetComponent<ChordInput>();
                var follow = cameraObject.AddComponent<TowerCamera>();
                follow.target = player.transform; follow.maxY = lab ? 3.2f : 14;
                var fields = new List<DemoLightField>();
                fields.Add(CreateLight("Green Refuge", LightColorType.Green, new Vector2(-4.8f, 2.5f), 2.5f, 0, input));
                if (lab)
                {
                    fields.Add(CreateLight("Blue Experiment", LightColorType.Blue, new Vector2(-.4f, 3.7f), 3.7f, 5.8f, input));
                    fields.Add(CreateLight("Red Experiment", LightColorType.Red, new Vector2(3.2f, 3.7f), 3.7f, 3.4f, input));
                }
                else
                {
                    fields.Add(CreateLight("Blue Gate I", LightColorType.Blue, new Vector2(0, 4), 3.7f, 6.5f, input));
                    fields.Add(CreateLight("Green Checkpoint", LightColorType.Green, new Vector2(4.3f, 6.3f), 2.1f, 0, input));
                    fields.Add(CreateLight("Red Gate I", LightColorType.Red, new Vector2(0, 9.1f), 3.5f, 6.5f, input));
                    fields.Add(CreateLight("Green Upper Refuge", LightColorType.Green, new Vector2(-4.2f,10.5f), 2.1f, 0, input));
                    fields.Add(CreateLight("Blue Gate II", LightColorType.Blue, new Vector2(1, 13.3f), 3.5f, -6.5f, input));
                    fields.Add(CreateLight("Red Gate II", LightColorType.Red, new Vector2(0, 18.3f), 5.2f, 6.5f, input));
                }
                var session = new GameObject("Demo Session").AddComponent<DemoSession>();
                session.Configure(player, fields.ToArray(), spawn);
                if (!lab)
                {
                    Marker("Checkpoint I", new Vector2(4.3f,4.75f), false, session);
                    Marker("Checkpoint II", new Vector2(-4.2f,8.95f), false, session);
                }
                Marker("Tower Exit", lab ? new Vector2(6, .6f) : new Vector2(-2.5f,16), true, session);
                CreateHUD(session, input, lab);
                EditorSceneManager.SaveScene(scene, path);
                Validate(scene, fields.Count);
            }
            finally
            {
                if (!Application.isBatchMode)
                { if (previous.IsValid()) SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
            }
        }
        static Sprite CreatePixel()
        {
            string path = Root + "/Art/Pixel.png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(8,8);
                var pixels = new Color[64]; for (int i=0;i<64;i++) pixels[i]=Color.white;
                texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single; importer.spritePixelsPerUnit=8;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        static GameObject Rect(string name, Vector2 position, Vector2 size, Color color, int order, Transform parent = null)
        {
            var obj=new GameObject(name,typeof(SpriteRenderer));
            obj.transform.SetParent(parent,false); obj.transform.position=position; obj.transform.localScale=new Vector3(size.x,size.y,1);
            var renderer=obj.GetComponent<SpriteRenderer>(); renderer.sprite=pixel; renderer.color=color; renderer.sortingOrder=order; renderer.sharedMaterial=unlit;
            return obj;
        }
        static GameObject Prop(string name, Vector2 position, Transform parent, int order=1, Color? tint=null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Cainos+name+".prefab");
            if(prefab==null) throw new InvalidOperationException("Missing Cainos prop: "+name);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            obj.transform.position=position;
            foreach(var r in obj.GetComponentsInChildren<SpriteRenderer>()) {r.sortingOrder=order; r.sharedMaterial=unlit; if(tint.HasValue) r.color=tint.Value;}
            foreach(var collider in obj.GetComponentsInChildren<Collider2D>()) collider.enabled=false;
            return obj;
        }
        static void Platform(string name,float x,float y,float width,Transform parent)
        {
            var platform=new GameObject(name); platform.transform.SetParent(parent,false); platform.transform.position=new Vector3(x,y,0);
            var collider=platform.AddComponent<BoxCollider2D>(); collider.size=new Vector2(width,.20f); collider.offset=new Vector2(0,-.1f); collider.usedByEffector=true;
            var effector=platform.AddComponent<PlatformEffector2D>(); effector.useOneWay=true; effector.useSideFriction=false;
            int count=Mathf.CeilToInt(width/2);
            for(int i=0;i<count;i++)
            {
                float part=width/count;
                var art=Prop("Platform 01 L X2",new Vector2(x-width*.5f+i*part,y),platform.transform,8);
                art.transform.localScale=new Vector3(part/2,1,1);
            }
            Rect("Safe stone edge",new Vector2(x,y-.025f),new Vector2(width,.04f),new Color(.61f,.65f,.63f),9,platform.transform);
        }
        static void BuildBackdrop(Transform camera,int height)
        {
            var far=new GameObject("Parallax - distant village").transform;
            var parallax=far.gameObject.AddComponent<TowerParallax>(); parallax.cameraTransform=camera; parallax.factor=.70f;
            for(int i=0;i<11;i++)
            {
                float x=-14+i*2.8f;
                Rect("Distant tower",new Vector2(x,8),new Vector2(1.6f,30),new Color(.055f,.085f,.12f),-50,far);
                for(int row=0;row<9;row++) Rect("Distant window",new Vector2(x,1+row*3.4f),new Vector2(.13f,.38f),new Color(.17f,.20f,.20f),-49,far);
            }
            var mid=new GameObject("Parallax - dungeon masonry").transform;
            var p=mid.gameObject.AddComponent<TowerParallax>(); p.cameraTransform=camera; p.factor=.25f;
            for(int row=-3;row<height+8;row+=2)
                for(int col=-2;col<=2;col++)
                    Prop("Brick Wall 01",new Vector2(col*4.1f+(row%4==0?.5f:0),row),mid,-30,new Color(.20f,.24f,.29f));
            var foreground=new GameObject("Parallax - watchtower pillars").transform;
            var front=foreground.gameObject.AddComponent<TowerParallax>(); front.cameraTransform=camera; front.factor=.04f;
            for(int side=-1;side<=1;side+=2)
            {
                Rect("Tower silhouette",new Vector2(side*7.7f,height*.5f),new Vector2(.9f,height+18),new Color(.035f,.043f,.058f),25,foreground);
                for(int y=0;y<height;y+=4) Prop("Banner",new Vector2(side*7,y+2),foreground,24,new Color(.42f,.34f,.42f));
            }
        }
        static void Props(Transform parent,bool lab)
        {
            Prop("Barrel",new Vector2(-6.1f,0),parent,10);
            Prop("Crate Small",new Vector2(-5.7f,0),parent,10);
            if(lab) return;
            Prop("Banner",new Vector2(5.4f,6),parent,-1);
            Prop("Bush 01",new Vector2(-4.8f,8.4f),parent,8);
            Prop("Barrel",new Vector2(4.9f,12.6f),parent,8);
        }
        static ChordMotor CreatePlayer(Vector2 spawn)
        {
            var obj=new GameObject("Shadow Creature"); obj.transform.position=spawn;
            var body=obj.AddComponent<Rigidbody2D>(); body.gravityScale=2.5f; body.freezeRotation=true;
            body.interpolation=RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            var capsule=obj.AddComponent<CapsuleCollider2D>(); capsule.size=new Vector2(.44f,.65f);
            string frictionPath=Root+"/Art/ShadowFriction.physicsMaterial2D";
            var friction=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(frictionPath);
            if(friction==null) {friction=new PhysicsMaterial2D("Shadow friction"){friction=0,bounciness=0}; AssetDatabase.CreateAsset(friction,frictionPath);}
            capsule.sharedMaterial=friction;
            Rect("Shadow rim",spawn,new Vector2(.49f,.68f),new Color(.27f,.43f,.5f),20,obj.transform);
            Rect("Shadow body",spawn,new Vector2(.43f,.63f),new Color(.025f,.032f,.051f),21,obj.transform);
            var eyeRoot=new GameObject("Eyes").transform; eyeRoot.SetParent(obj.transform,false);
            Rect("Left eye",spawn+new Vector2(-.10f,.08f),new Vector2(.065f,.095f),new Color(.84f,1,1),22,eyeRoot);
            Rect("Right eye",spawn+new Vector2(.10f,.08f),new Vector2(.065f,.095f),new Color(.84f,1,1),22,eyeRoot);
            obj.AddComponent<ChordInput>(); obj.AddComponent<ChordAudio>();
            var motor=obj.AddComponent<ChordMotor>(); motor.Configure(eyeRoot); return motor;
        }
        static DemoLightField CreateLight(string name,LightColorType kind,Vector2 position,float length,float bend,ChordInput input)
        {
            // Clone the existing project's light prototype, retaining its script components.
            var preview=EditorSceneManager.OpenPreviewScene(Prototype);
            GameObject clone=null;
            try
            {
                foreach(var root in preview.GetRootGameObjects())
                {
                    var light=root.GetComponentInChildren<LightController>();
                    if(light!=null) {clone=UnityEngine.Object.Instantiate(light.gameObject); break;}
                }
                if(clone==null) throw new InvalidOperationException("Light_prototype has no LightController.");
                SceneManager.MoveGameObjectToScene(clone,SceneManager.GetActiveScene());
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
            clone.name=name; clone.transform.position=position; clone.transform.rotation=Quaternion.identity; clone.transform.localScale=Vector3.one;
            foreach(var debug in clone.GetComponentsInChildren<DebugLightInput>()) UnityEngine.Object.DestroyImmediate(debug);
            var controller=clone.GetComponent<LightController>();
            string assetPath=Root+"/Data/"+name.Replace(" ","")+".asset";
            var profile=AssetDatabase.LoadAssetAtPath<LightProfile>(assetPath);
            if(profile==null)
            {
                profile=controller.profile!=null ? UnityEngine.Object.Instantiate(controller.profile) : ScriptableObject.CreateInstance<LightProfile>();
                profile.lightColor=kind; profile.beamLength=length; profile.beamWidth=kind==LightColorType.Green?1.25f:.6f;
                profile.maxBend=bend; profile.chordPersonality=true; AssetDatabase.CreateAsset(profile,assetPath);
            }
            controller.profile=profile;
            var spline=clone.GetComponent<BeamSpline>(); spline.pointCount=25; spline.length=length; spline.Rebuild();
            var contour=clone.GetComponent<ShadowGenerator>(); contour.shadowWidth=profile.beamWidth*.5f;
            Color color=kind==LightColorType.Green ? new Color(.3f,1,.65f) : kind==LightColorType.Blue ? new Color(.23f,.66f,1) : new Color(1,.22f,.35f);
            foreach(var line in clone.GetComponentsInChildren<LineRenderer>())
            {
                line.sharedMaterial=unlit; line.useWorldSpace=false; line.widthMultiplier=line.gameObject==clone?.055f:.025f;
                line.widthCurve=AnimationCurve.Constant(0,1,1);
                line.startColor=line.endColor=color; line.sortingOrder=15;
                line.positionCount=0; line.numCapVertices=3;
            }
            var area=new GameObject("Visible exposure boundary",typeof(MeshFilter),typeof(MeshRenderer));
            area.transform.SetParent(clone.transform,false);
            area.GetComponent<MeshRenderer>().sharedMaterial=unlit; area.GetComponent<MeshRenderer>().sortingOrder=12;
            var visual=area.AddComponent<BeamAreaVisual>(); visual.source=contour; visual.tint=new Color(color.r,color.g,color.b,.25f);
            var lamp=Rect("Light emitter",position,new Vector2(.52f,.16f),color,18,clone.transform);
            var glow=lamp.AddComponent<Light2D>(); glow.lightType=Light2D.LightType.Point; glow.color=color; glow.intensity=.65f; glow.pointLightOuterRadius=1.5f;
            var field=clone.AddComponent<DemoLightField>(); field.Configure(input); return field;
        }
        static void Marker(string name,Vector2 position,bool goal,DemoSession session)
        {
            var marker=Prop("Stone of Recall",position-new Vector2(0,.5f),null,10);
            marker.name=name;
            var trigger=marker.AddComponent<BoxCollider2D>(); trigger.size=new Vector2(.7f,1); trigger.offset=new Vector2(0,.5f); trigger.isTrigger=true;
            var component=marker.AddComponent<DemoMarker>(); component.session=session; component.goal=goal; component.respawn=position;
        }
        static void CreateHUD(DemoSession session,ChordInput input,bool lab)
        {
            var canvasObj=new GameObject("Chord HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            canvasObj.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObj.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
            var panel=Panel("Header",canvasObj.transform,new Vector2(0,1),new Vector2(1,1),new Vector2(0,1),new Vector2(0,0),new Vector2(0,105),new Color(.025f,.04f,.07f,.94f));
            Text("Title",panel.transform,lab?"LUMENWAVE / LIGHT LAB":"LUMENWAVE / THE WATCHTOWER",new Vector2(25,-20),new Vector2(800,32),24,new Color(.88f,.9f,.79f));
            var status=Text("Status",panel.transform,"SHADOW 100",new Vector2(25,-60),new Vector2(400,24),17,Color.white);
            var notes=Text("Notes",panel.transform,"C   E   G",new Vector2(510,-60),new Vector2(700,24),17,new Color(.6f,.74f,.82f));
            var health=Panel("Health",panel.transform,new Vector2(1,1),new Vector2(1,1),new Vector2(1,1),new Vector2(-24,-26),new Vector2(250,9),new Color(.18f,.25f,.3f));
            var fill=Panel("Fill",health.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Vector2.zero,new Color(.4f,.95f,.72f)).GetComponent<UnityEngine.UI.Image>();
            fill.type=UnityEngine.UI.Image.Type.Filled; fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
            var footer=Panel("Footer",canvasObj.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,Vector2.zero,new Vector2(0,100),new Color(.025f,.04f,.07f,.94f));
            Text("Controls",footer.transform,"Q + W  LEFT     O + P  RIGHT     + SPACE  JUMP / RED FOLD     R  RETRY",new Vector2(25,-16),new Vector2(1200,27),18,Color.white);
            Text("Rules",footer.transform,"C + E bends BLUE / C + E + G folds RED / effects last 3s / GREEN heals",new Vector2(25,-46),new Vector2(1200,24),16,new Color(.6f,.74f,.82f));
            var message=Text("Hint",footer.transform,"",new Vector2(25,-72),new Vector2(1200,23),16,new Color(.87f,.77f,.5f));
            var hud=canvasObj.AddComponent<DemoHud>(); hud.session=session; hud.input=input; hud.status=status; hud.notes=notes; hud.message=message; hud.healthFill=fill;
        }
        static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); obj.transform.SetParent(parent,false);
            var rect=(RectTransform)obj.transform; rect.anchorMin=min; rect.anchorMax=max; rect.pivot=pivot; rect.anchoredPosition=pos; rect.sizeDelta=size;
            var image=obj.GetComponent<UnityEngine.UI.Image>(); image.color=color; image.raycastTarget=false; image.sprite=pixel; return obj;
        }
        static TMP_Text Text(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform)); obj.transform.SetParent(parent,false);
            var rect=(RectTransform)obj.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=obj.AddComponent<TextMeshProUGUI>(); text.font=font; text.fontSize=fontSize; text.text=value; text.color=color; text.raycastTarget=false;
            text.enableAutoSizing=false; text.textWrappingMode=TextWrappingModes.NoWrap; return text;
        }
        static void Validate(Scene scene,int expectedFields)
        {
            int players=0, fields=0, missing=0;
            foreach(var root in scene.GetRootGameObjects())
            {
                players+=root.GetComponentsInChildren<ChordMotor>().Length;
                fields+=root.GetComponentsInChildren<DemoLightField>().Length;
                foreach(var t in root.GetComponentsInChildren<Transform>()) missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach(var player in root.GetComponentsInChildren<ChordMotor>())
                    foreach(var sprite in player.GetComponentsInChildren<SpriteRenderer>())
                        if(sprite.sprite==null) throw new InvalidOperationException("Missing player sprite: "+sprite.name);
            }
            if(players!=1 || fields!=expectedFields || missing!=0) throw new InvalidOperationException("Demo scene structure check failed.");
            Debug.Log("PASS scene structure: "+scene.path+" / lights="+fields+" / no missing scripts");
        }
    }
}
