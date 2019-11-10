
using ZooKeeperNet;

using S22.Sasl;
using System.Net;

namespace SmokeTest
{
    internal class S22SaslClient : ISaslClient
    {
        private SaslMechanism m = null;

        public S22SaslClient()
        {
        }

        public byte[] Start(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            m = SaslFactory.Create("DIGEST-MD5");

            m.Properties.Add("Username", "bob");
            m.Properties.Add("Password", "bobsecret");
            m.Properties.Add("Protocol", "zookeeper");

            return new byte[] { };
        }

        public bool IsCompleted
        {
            get
            {
                return m == null || m.IsCompleted;
            }
        }

        public bool HasLastPacket
        {
            get
            {
                return false;
            }
        }

        public byte[] EvaluateChallenge(byte[] token)
        {
            return m.GetResponse(token);
        }

        public void Finish()
        {
            m = null;
        }
    }
}
