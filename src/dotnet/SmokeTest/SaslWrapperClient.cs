using System;

using ZooKeeperNet;
using System.Net;
using SASLWrapper;

namespace SmokeTest
{
    internal class SaslWrapperClient : ISaslClient, IDisposable
    {
        private Client client = null;
        private string mechlist;
        private ClientParams clientParams;
        private bool useServerFQDN;
        private string mech;
        private bool mustContinue;

        public SaslWrapperClient(string mechlist, ClientParams clientParams, bool useServerFQDN)
        {
            this.mechlist = mechlist;
            this.clientParams = clientParams;
            this.useServerFQDN = useServerFQDN;
        }

        public bool IsCompleted => client != null && !mustContinue;

        public bool HasLastPacket => this.mech == "GSSAPI";

        public byte[] Start(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            byte[] clientout;

            DisposeClient();

            clientParams.IpLocalPort = string.Format("{0};{1}", localEndPoint.Address, localEndPoint.Port);
            clientParams.IpRemotePort = string.Format("{0};{1}", remoteEndPoint.Address, remoteEndPoint.Port);

            if (useServerFQDN)
            {
                clientParams.ServerFQDN = Dns.GetHostEntry(remoteEndPoint.Address).HostName;
            }

            //Console.WriteLine("CreateClient: clientParams: {0}", clientParams);

            client = Library.Instance.CreateClient(clientParams);

            //Console.WriteLine("Client.Start...");
            mustContinue = client.Start(mechlist, out clientout, out mech);
            Console.WriteLine("Client.Start: mech: {0}, mustContinue: {1}, clientout: {2} bytes", mech, mustContinue, clientout != null ? clientout.Length : 0);

            return clientout;
        }

        public byte[] EvaluateChallenge(byte[] challenge)
        {
            byte[] clientout;

            //Console.WriteLine("Client.Step...");
            mustContinue = client.Step(challenge, out clientout);
            Console.WriteLine("Client.Step: mustContinue: {0}, clientout: {1} bytes", mustContinue, clientout != null ? clientout.Length : 0);

            return clientout;
        }

        public void Finish()
        {
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        private void DisposeClient()
        {
            if (client != null)
            {
                client.Dispose();
            }
            client = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    DisposeClient();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
