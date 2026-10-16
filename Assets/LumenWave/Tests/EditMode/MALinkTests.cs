using System.Net;
using System.Net.Sockets;
using LumenWave.MA;
using NUnit.Framework;
using UnityEngine;

namespace LumenWave.Tests
{
    public class MALinkTests
    {
        [Test] public void Osc_Encoding_IsPaddedAndBigEndian()
        {
            var b = Osc.Encode("/gma3/cmd", "Go+ Exec 1");
            Assert.AreEqual(0, b.Length % 4);
            // "/gma3/cmd" is 9 chars → padded to 12, then ",s\0\0" (4), then "Go+ Exec 1" 10 → 12.
            Assert.AreEqual(12 + 4 + 12, b.Length);
            Assert.AreEqual((byte)',', b[12]);
            Assert.AreEqual((byte)'s', b[13]);
            var i = Osc.Encode("/x", 258);
            Assert.AreEqual(new byte[] { 0, 0, 1, 2 }, new[] { i[8], i[9], i[10], i[11] });
        }

        [Test] public void Osc_RoundTrip()
        {
            var b = Osc.Encode("/gma3/cmd", "Go+ Exec 201", 7, 0.5f);
            Assert.IsTrue(Osc.TryDecode(b, out var addr, out var args));
            Assert.AreEqual("/gma3/cmd", addr);
            Assert.AreEqual("Go+ Exec 201", args[0]);
            Assert.AreEqual(7, args[1]);
            Assert.AreEqual(0.5f, (float)args[2], 1e-6f);
        }

        [Test] public void UdpListener_ReceivesGoExecPacket()
        {
            using (var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                int port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
                listener.Client.ReceiveTimeout = 2000;
                var go = new GameObject("malink-test");
                try
                {
                    var link = go.AddComponent<MALink>();
                    link.config = new MALinkConfig { enabled = true, ip = "127.0.0.1", port = port, prefix = "gma3", throttle = 0f };
                    Assert.IsTrue(link.GoExec(42, "test"));
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = listener.Receive(ref from);
                    Assert.IsTrue(Osc.TryDecode(data, out var addr, out var args));
                    Assert.AreEqual("/gma3/cmd", addr);
                    Assert.AreEqual("Go+ Exec 42", args[0]);
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test] public void Disabled_SendsNothing_AndOfflineIsSafe()
        {
            var go = new GameObject("malink-test2");
            try
            {
                var link = go.AddComponent<MALink>();
                link.config = new MALinkConfig { enabled = false };
                Assert.IsFalse(link.GoExec(1, "x"));
                // Unreachable endpoint: must not throw.
                link.config = new MALinkConfig { enabled = true, ip = "10.255.255.1", port = 9, throttle = 0f };
                Assert.DoesNotThrow(() => link.GoExec(2, "offline"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
