/*
 *  Licensed to the Apache Software Foundation (ASF) under one or more
 *  contributor license agreements.  See the NOTICE file distributed with
 *  this work for additional information regarding copyright ownership.
 *  The ASF licenses this file to You under the Apache License, Version 2.0
 *  (the "License"); you may not use this file except in compliance with
 *  the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */
namespace ZooKeeperNet.Tests
{
    using System;
    using NUnit.Framework;
    using Org.Apache.Zookeeper.Data;
    using ZooKeeperNet;
    using S22.Sasl;
    using System.Net;
    using System.Collections.Generic;

    class S22SaslClient : ISaslClient
    {
        // The following must be configured in zoo.conf:
        //
        //     authProvider.1=org.apache.zookeeper.server.auth.SASLAuthenticationProvider
        //
        // The following in jaas.conf:
        //
        //     Server {
        //       org.apache.zookeeper.server.auth.DigestLoginModule required
        //         user_super="adminsecret"
        //         user_bob="bobsecret";
        //     };
        //
        // And the server must be started with:
        //
        //     -Djava.security.auth.login.config=.../jaas.conf
        //
        private const string Username = "bob";
        private const string Password = "bobsecret";

        private SaslMechanism m = null;

        public byte[] Start(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint)
        {
            m = SaslFactory.Create("DIGEST-MD5");

            m.Properties.Add("Username", Username);
            m.Properties.Add("Password", Password);
            m.Properties.Add("Protocol", "zookeeper");

            // Client start is empty.
            return null;
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
                return false; // not GSSAPI.
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

    [TestFixture]
    public class SaslTests : AbstractZooKeeperTests
    {
        [Test]
        public void testSasl()
        {
            string name = "/" + Guid.NewGuid() + "sasltest";

            using (var zk = CreateClientWithSasl(new S22SaslClient()))
            {
                List<ACL> acl = new List<ACL>();
                acl.Add(new ACL(Perms.ALL, new ZKId("sasl", "bob")));

                Assert.AreEqual(name, zk.Create(name, new byte[0], acl, CreateMode.Persistent));
            }

            using (var zk = CreateClient())
            {
                try
                {
                    zk.GetData(name, false, new Stat());
                    Assert.Fail("Should have received a permission error");
                }
                catch (KeeperException e)
                {
                    Assert.AreEqual(KeeperException.Code.NOAUTH, e.ErrorCode);
                }
            }

            using (var zk = CreateClientWithSasl(new S22SaslClient()))
            {
                zk.GetData(name, false, new Stat());
            }
        }
    }
}
