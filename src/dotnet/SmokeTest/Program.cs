using System;
using System.Text;
using System.Threading;

using ZooKeeperNet;

namespace SmokeTest
{
    public class TestCase
    {
        public string Mech;
        public SASLWrapper.ClientParams SaslWrapperParams;
        public RdkafkaSaslSspiWrapper.ClientParams RdKafkaParams;
        public bool UseServerFQDN;
        public string NodePath;

        public static readonly TestCase DIGEST = new TestCase
        {
            Mech = "DIGEST-MD5",
            SaslWrapperParams = new SASLWrapper.ClientParams
            {
                Service = "zookeeper",
                ServerFQDN = "zk-sasl-md5",
                Authname = () => "bob",
                User = () => "bob",
                Pass = () => "bobsecret"
            },
            UseServerFQDN = false,
            NodePath = "/foo"
        };

        public static readonly TestCase GSSAPI = new TestCase
        {
            Mech = "GSSAPI",
            SaslWrapperParams = new SASLWrapper.ClientParams
            {
                Service = "zookeeper",
                Authname = () => "zkcli@CROSSTWINE.COM",
                User = () => "zkcli@CROSSTWINE.COM"
            },
            UseServerFQDN = true,
            NodePath = "/krb",
            RdKafkaParams = new RdkafkaSaslSspiWrapper.ClientParams
            {
                Mechanisms = "GSSAPI",
                Service = "zookeeper",
                // Realm = ""
            }
        };
    }

    class Program
    {
        static void TestGetData(IZooKeeper zk, string path)
        {
            byte[] data = zk.GetData(path, null, null);

            Console.WriteLine("Data at {0}: '{1}'",
                              path,
                              Encoding.ASCII.GetString(data));
        }

        static void Main(string[] args)
        {
            string connString = "192.168.0.3";
            string impl = "rdkafka";
            TestCase testCase = TestCase.GSSAPI;
            ISaslClient saslClient;

            if (args.Length > 0)
            {
                connString = args[0];
            }

            if (args.Length > 1)
            {
                impl = args[1];
            }

            if (impl == "s22")
            {
                saslClient = new S22SaslClient();
            }
            else if (impl == "cyrus")
            {
                SASLWrapper.Library.NeedWSAStartup = false;
                SASLWrapper.Library.Log = (int level, string message) => Console.WriteLine(">>>[{0}] {1}", level, message);

                saslClient = new SaslWrapperClient(testCase.Mech, testCase.SaslWrapperParams, testCase.UseServerFQDN);
            }
            else if (impl == "rdkafka")
            {
                RdkafkaSaslSspiWrapper.ClientParams clientParams = testCase.RdKafkaParams;

                clientParams.Log = (int level, string fac, string message) => Console.WriteLine(">>>[{0} {1}] {2}", level, fac, message);

                saslClient = new RdkafkaSaslSspiWrapperClient(clientParams);
            }
            else
            {
                throw new Exception(string.Format("Unrecognized impl {0}", impl));
            }

            try
            {
                TestSaslZooKeeper(connString, saslClient, testCase.NodePath);
            }
            finally
            {
                if (saslClient is IDisposable)
                {
                    (saslClient as IDisposable).Dispose();
                }
            }
        }

        private static void TestSaslZooKeeper(string connString, ISaslClient saslClient, string protectedPath)
        {
            ZooKeeper zkx = new ZooKeeper(connString, new TimeSpan(0, 0, 30),
                                          null, saslClient);
            IZooKeeper zk = zkx;

            Thread.Sleep(500);

            //TestGetData(zk, "/baz");

            try
            {
                TestGetData(zk, protectedPath);
            }
            catch (KeeperException.NoAuthException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            TestGetData(zk, protectedPath);

            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine("Sleeping a bit");
                Thread.Sleep(1000);

                try
                {
                    // TestGetData(zk, "/baz");
                    TestGetData(zk, protectedPath);
                }
                catch (KeeperException ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
    }
}
