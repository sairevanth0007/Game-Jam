using UnityEngine;

namespace LumenWave
{
    /// <summary>Installs the audio director on the persistent root and sound powers on Umbra.</summary>
    static class SoundBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameSystems.Register(root =>
            {
                if (root.GetComponent<AudioDirector>() == null) root.AddComponent<AudioDirector>();
            });
            GameEvents.PlayerSpawned += OnPlayerSpawned;
        }

        static void OnPlayerSpawned(GameObject player)
        {
            if (player != null && player.GetComponent<SoundPowers>() == null) player.AddComponent<SoundPowers>();
        }
    }
}
