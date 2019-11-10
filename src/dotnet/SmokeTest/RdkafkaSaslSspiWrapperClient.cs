using System;

using ZooKeeperNet;
using System.Net;
using RdkafkaSaslSspiWrapper;

namespace SmokeTest
{
    internal class RdkafkaSaslSspiWrapperClient : ISaslClient, IDisposable
    {
        private Client client = null;
        private ClientParams clientParams;
        private bool isComplete;

        public RdkafkaSaslSspiWrapperClient(ClientParams clientParams)
        {
            this.clientParams = clientParams;
        }

        public bool IsCompleted => client != null && isComplete;

        public bool HasLastPacket => false;

        public byte[] Start(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            DisposeClient();

            clientParams.Hostname = Dns.GetHostEntry(remoteEndPoint.Address).HostName;

            Console.WriteLine("CreateClient: clientParams: {0}", clientParams);

            isComplete = false;
            client = Library.Instance.CreateClient(clientParams);

            byte[] clientout;
            if (client.InitialPacket(out clientout))
            {
                Console.WriteLine("Client.Start: clientout: {0} bytes", clientout != null ? clientout.Length : 0);
                return clientout;
            }

            throw new Exception("Client.Start: No initial packet?!");
        }

        public byte[] EvaluateChallenge(byte[] challenge)
        {
            byte[] clientout;

            Console.WriteLine("Client.Step: challenge: {0} bytes", challenge != null ? challenge.Length : 0);
            bool mustContinue = client.Step(challenge, out clientout);
            Console.WriteLine("Client.Step: mustContinue: {0}, clientout: {1} bytes", mustContinue, clientout != null ? clientout.Length : 0);

            isComplete = !mustContinue;

            return clientout;
        }

        public void Finish()
        {
            DisposeClient();
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
