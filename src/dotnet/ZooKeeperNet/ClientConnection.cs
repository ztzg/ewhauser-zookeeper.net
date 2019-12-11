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

namespace ZooKeeperNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using log4net;
    using Org.Apache.Jute;
    using Org.Apache.Zookeeper.Proto;

    public class ClientConnection : IClientConnection
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(ClientConnection));

        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromMilliseconds(500);        
        private static bool disableAutoWatchReset = false;
        private static int maximumPacketLength = 1024 * 1024 * 4;
        private static int maximumSpin = 30;

        /// <summary>
        /// Gets a value indicating how many times we should spin during waits.
        /// Defaults to 30.
        /// </summary>
        public static int MaximumSpin
        {
            get { return maximumSpin; }
            set
            {
                if (value <= 0)
                {
                    string message = string.Format("Cannot set property '{0}' to less than zero. Value is: {1}.", "MaximumSpin", value);
                    throw new InvalidOperationException(message);
                }

                maximumSpin = value;
            }
        }

        /// <summary>
        /// Gets a value indicating the maximum packet length allowed.
        /// Defaults to 4,194,304 (4MB).
        /// </summary>
        public static int MaximumPacketLength
        {
            get { return maximumPacketLength; }
            set
            {
                if (value <= 0)
                {
                    string message = string.Format("Cannot set property '{0}' to less than zero. Value is: {1}.", "MaximumPacketLength", value);
                    throw new InvalidOperationException(message);
                }

                maximumPacketLength = value;
                
            }
        }

        /// <summary>
        /// Gets a value indicating if auto watch reset should be disabled or not.
        /// Defaults to <c>false</c>.
        /// </summary>
        public static bool DisableAutoWatchReset
        {
            get { return disableAutoWatchReset; }
            set { disableAutoWatchReset = value; }
        }

        //static ClientConnection()
        //{
        //    // this var should not be public, but otw there is no easy way
        //    // to test
        //    var zkSection = (System.Collections.Specialized.NameValueCollection)System.Configuration.ConfigurationManager.GetSection("zookeeper");
        //    if (zkSection != null)
        //    {
        //        Boolean.TryParse(zkSection["disableAutoWatchReset"], out disableAutoWatchReset);
        //    }
        //    if (LOG.IsDebugEnabled)
        //    {
        //        LOG.DebugFormat("zookeeper.disableAutoWatchReset is {0}",disableAutoWatchReset);
        //    }
        //    //packetLen = Integer.getInteger("jute.maxbuffer", 4096 * 1024);
        //    packetLen = 4096 * 1024;
        //}

        internal string hosts;
        internal readonly ZooKeeper zooKeeper;
        internal readonly ZKWatchManager watcher;
        internal readonly ISaslClient saslClient;
        internal readonly List<IPEndPoint> serverAddrs = new List<IPEndPoint>();
        internal readonly List<AuthData> authInfo = new List<AuthData>();
        internal TimeSpan readTimeout;
        
        private int isClosed;
        public bool IsClosed
        {
            get
            {
                return Interlocked.CompareExchange(ref isClosed, 0, 0) == 1;
            }
        }

        internal ClientConnectionRequestProducer producer;
        internal ClientConnectionEventConsumer consumer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConnection"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="zooKeeper">The zoo keeper.</param>
        /// <param name="watcher">The watch manager.</param>
        /// <param name="saslClient">The SASL client.</param>
        public ClientConnection(string connectionString, TimeSpan sessionTimeout, ZooKeeper zooKeeper, ZKWatchManager watcher, ISaslClient saslClient) :
            this(connectionString, sessionTimeout, zooKeeper, watcher, saslClient, 0, new byte[16], DefaultConnectTimeout)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConnection"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="zooKeeper">The zoo keeper.</param>
        /// <param name="watcher">The watch manager.</param>
        /// <param name="connectTimeout">Connection Timeout.</param>
        public ClientConnection(string connectionString, TimeSpan sessionTimeout, ZooKeeper zooKeeper, ZKWatchManager watcher, TimeSpan connectTimeout) :
            this(connectionString, sessionTimeout, zooKeeper, watcher, 0, new byte[16], connectTimeout)
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConnection"/> class.
        /// </summary>
        /// <param name="hosts">The hosts.</param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="zooKeeper">The zoo keeper.</param>
        /// <param name="watcher">The watch manager.</param>
        /// <param name="sessionId">The session id.</param>
        /// <param name="sessionPasswd">The session passwd.</param>
        public ClientConnection(string hosts, TimeSpan sessionTimeout, ZooKeeper zooKeeper, ZKWatchManager watcher, long sessionId, byte[] sessionPasswd)
            : this(hosts, sessionTimeout, zooKeeper, watcher, 0, new byte[16], DefaultConnectTimeout)
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConnection"/> class.
        /// </summary>
        /// <param name="hosts">The hosts.</param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="zooKeeper">The zoo keeper.</param>
        /// <param name="watcher">The watch manager.</param>
        /// <param name="saslClient">The SASL client.</param>
        /// <param name="sessionId">The session id.</param>
        /// <param name="sessionPasswd">The session passwd.</param>
        public ClientConnection(string hosts, TimeSpan sessionTimeout, ZooKeeper zooKeeper, ZKWatchManager watcher, ISaslClient saslClient, long sessionId, byte[] sessionPasswd)
            : this(hosts, sessionTimeout, zooKeeper, watcher, saslClient, 0, new byte[16], DefaultConnectTimeout)
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConnection"/> class.
        /// </summary>
        /// <param name="hosts">The hosts.</param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="zooKeeper">The zoo keeper.</param>
        /// <param name="watcher">The watch manager.</param>
        /// <param name="sessionId">The session id.</param>
        /// <param name="sessionPasswd">The session passwd.</param>
        /// <param name="connectTimeout">Connection Timeout.</param>
        public ClientConnection(string hosts, TimeSpan sessionTimeout, ZooKeeper zooKeeper, ZKWatchManager watcher, long sessionId, byte[] sessionPasswd, TimeSpan connectTimeout) :
            this(hosts, sessionTimeout, zooKeeper, watcher, null, sessionId, sessionPasswd, connectTimeout)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientConnection"/> class.
        /// </summary>
        /// <param name="hosts">The hosts.</param>
        /// <param name="sessionTimeout">The session timeout.</param>
        /// <param name="zooKeeper">The zoo keeper.</param>
        /// <param name="watcher">The watch manager.</param>
        /// <param name="saslClient">The SASL client.</param>
        /// <param name="sessionId">The session id.</param>
        /// <param name="sessionPasswd">The session passwd.</param>
        /// <param name="connectTimeout">Connection Timeout.</param>
        public ClientConnection(string hosts, TimeSpan sessionTimeout, ZooKeeper zooKeeper, ZKWatchManager watcher, ISaslClient saslClient, long sessionId, byte[] sessionPasswd, TimeSpan connectTimeout)
        {
            this.hosts = hosts;
            this.zooKeeper = zooKeeper;
            this.watcher = watcher;
            this.saslClient = saslClient;
            SessionTimeout = sessionTimeout;
            SessionId = sessionId;
            SessionPassword = sessionPasswd;
            ConnectionTimeout = connectTimeout;

            // parse out chroot, if any
            hosts = SetChrootPath();
            GetHosts(hosts);
            SetTimeouts(sessionTimeout);
            CreateConsumer();
            CreateProducer();
        }

        private void CreateConsumer()
        {
            consumer = new ClientConnectionEventConsumer(this);
        }

        private void CreateProducer()
        {
            producer = new ClientConnectionRequestProducer(this);
        }

        private string SetChrootPath()
        {
            int off = hosts.IndexOf(PathUtils.PathSeparatorChar);
            if (off >= 0)
            {
                string path = hosts.Substring(off);
                // ignore "/" chroot spec, same as null
                if (path.Length == 1)
                    ChrootPath = null;
                else
                {
                    PathUtils.ValidatePath(path);
                    ChrootPath = path;
                }
                hosts = hosts.Substring(0, off);
            }
            else
                ChrootPath = null;
            return hosts;
        }

        private void GetHosts(string hostLst)
        {
            string[] hostsList = hostLst.Split(',');
            List<IPEndPoint> nonRandomizedServerAddrs = new List<IPEndPoint>();
            foreach (string h in hostsList)
            {
                string host = h;
                int port = 2181;
                int pidx = h.LastIndexOf(':');
                if (pidx >= 0)
                {
                    // otherwise : is at the end of the string, ignore
                    if (pidx < h.Length - 1)
                    {
                        port = Int32.Parse(h.Substring(pidx + 1));
                    }
                    host = h.Substring(0, pidx);
                }

                // Handle dns-round robin or hostnames instead of IP addresses
                var hostIps = ResolveHostToIpAddresses(host);
                foreach (var ip in hostIps)
                {
                    nonRandomizedServerAddrs.Add(new IPEndPoint(ip, port));
                }
            }
            IEnumerable<IPEndPoint> randomizedServerAddrs 
                = nonRandomizedServerAddrs.OrderBy(s => Guid.NewGuid()); //Random order the servers

            serverAddrs.AddRange(randomizedServerAddrs);
        }

        private IEnumerable<IPAddress> ResolveHostToIpAddresses(string host)
        {
            // if the host represents an explicit IP address, use it directly. otherwise
            // lookup the name into it's IP addresses
            IPAddress parsedAddress;
            if (IPAddress.TryParse(host, out parsedAddress))
            {
                yield return parsedAddress;
            }
            else
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(host);
                IEnumerable<IPAddress> addresses = hostEntry.AddressList.Where(IsAllowedAddressFamily);
                foreach (IPAddress address in addresses)
                {
                    yield return address;
                }
            }
        }

        /// <summary>
        /// Checks to see if the specified address has an allowable address family.
        /// </summary>
        /// <param name="address">The adress to check.</param>
        /// <returns>
        /// <c>true</c> if the address is in the allowable address families, otherwise <c>false</c>.
        /// </returns>
        private static bool IsAllowedAddressFamily(IPAddress address)
        {
            // in the future, IPv6 (AddressFamily.InterNetworkV6) should be supported
            return address.AddressFamily == AddressFamily.InterNetwork;
        }

        private void SetTimeouts(TimeSpan sessionTimeout)
        {
            //since we have no need of it just remark it
            //connectTimeout = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(sessionTimeout.TotalMilliseconds / serverAddrs.Count));
            readTimeout = new TimeSpan(0, 0, 0, 0, Convert.ToInt32(sessionTimeout.TotalMilliseconds * 2 / 3));
        }

        /// <summary>
        /// Gets or sets the session timeout.
        /// </summary>
        /// <value>The session timeout.</value>
        public TimeSpan SessionTimeout { get; private set; }


        /// <summary>
        /// Gets or sets the connection timeout.
        /// </summary>
        /// <value>The connection timeout.</value>
        public TimeSpan ConnectionTimeout { get; private set; }


        /// <summary>
        /// Gets or sets the session password.
        /// </summary>
        /// <value>The session password.</value>
        public byte[] SessionPassword { get; internal set; }

        
        /// <summary>
        /// Gets or sets the session id.
        /// </summary>
        /// <value>The session id.</value>
        public long SessionId { get; internal set; }

        /// <summary>
        /// Gets or sets the chroot path.
        /// </summary>
        /// <value>The chroot path.</value>
        public string ChrootPath { get; private set; }

        public void Start()
        {
            consumer.Start();
            producer.Start();
        }

        public void AddAuthInfo(string scheme, byte[] auth)
        {
            if (!zooKeeper.State.IsAlive())
                return;
            authInfo.Add(new AuthData(scheme, auth));
            QueuePacket(new RequestHeader(-4, (int)OpCode.Auth), null, new AuthPacket(0, scheme, auth), null, null, null, null, null, null);
        }

        public ReplyHeader SubmitRequest(RequestHeader h, IRecord request, IRecord response, ZooKeeper.WatchRegistration watchRegistration)
        {
            ReplyHeader r = new ReplyHeader();
            Packet p = QueuePacket(h, r, request, response, null, null, watchRegistration, null, null);
            
            if (!p.WaitUntilFinishedSlim(SessionTimeout))
            {
                throw new TimeoutException(new StringBuilder("The request ").Append(request).Append(" timed out while waiting for a response from the server.").ToString());
            }
            return r;
        }

        public Packet QueuePacket(RequestHeader h, ReplyHeader r, IRecord request, IRecord response, string clientPath, string serverPath, ZooKeeper.WatchRegistration watchRegistration, object callback, object ctx)
        {
            return producer.QueuePacket(h, r, request, response, clientPath, serverPath, watchRegistration);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        private void InternalDispose()
        {
            if(Interlocked.CompareExchange(ref isClosed,1,0) == 0)
            {
                //closing = true;
                if (LOG.IsDebugEnabled)
                    LOG.DebugFormat("Closing client for session: 0x{0:X}", SessionId);

                try
                {
                    SubmitRequest(new RequestHeader { Type = (int)OpCode.CloseSession }, null, null, null);
                    SpinWait spin = new SpinWait();
                    DateTime timeoutAt = DateTime.UtcNow.Add(SessionTimeout);
                    while (!producer.IsConnectionClosedByServer)
                    {
                        spin.SpinOnce();
                        if (spin.Count > MaximumSpin)
                        {
                            if (timeoutAt <= DateTime.UtcNow)
                            {
                                throw new TimeoutException(
                                    string.Format("Timed out in Dispose() while closing session: 0x{0:X}", SessionId));
                            }
                            spin.Reset();
                        }
                    }
                }
                catch (ThreadInterruptedException)
                {
                    // ignore, close the send/event threads
                }
                catch (Exception ex)
                {
                    LOG.WarnFormat("Error disposing {0} : {1}", this.GetType().FullName, ex.Message);
                }
                finally
                {
                    if (null != producer)
                    {
                        producer.Dispose();
                    }
                    
                    if (null != consumer)
                    {
                        consumer.Dispose();
                    }
                }

            }
        }
        public void Dispose()
        {
            InternalDispose();
            GC.SuppressFinalize(this);
        }

        ~ClientConnection()
        {
            InternalDispose();
        }

        /// <summary>
        /// Returns a <see cref="System.string"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.string"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("sessionid:0x").AppendFormat("{0:X}", SessionId)
                .Append(" lastZxid:").Append(producer.lastZxid)
                .Append(" xid:").Append(producer.xid)
                .Append(" sent:").Append(producer.sentCount)
                .Append(" recv:").Append(producer.recvCount)
                .Append(" queuedpkts:").Append(producer.OutgoingQueueCount)
                .Append(" pendingresp:").Append(producer.PendingQueueCount)
                .Append(" queuedevents:").Append(consumer.waitingEvents.Count);

            return sb.ToString();
        }

        internal class AuthData
        {
            public string Scheme
            {
                get;
                private set;
            }

            private byte[] data;

            public byte[] GetData()
            {
                return data;
            }

            public AuthData(string scheme, byte[] data)
            {
                this.Scheme = scheme;
                this.data = data;
            }

        }

        internal class WatcherSetEventPair
        {
            public IEnumerable<IWatcher> Watchers
            {
                get;
                private set;
            }
            public WatchedEvent WatchedEvent
            {
                get;
                private set;
            }

            public WatcherSetEventPair(IEnumerable<IWatcher> watchers, WatchedEvent @event)
            {
                this.Watchers = watchers;
                this.WatchedEvent = @event;
            }
        }
    }
}
