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
using System.Net;

namespace ZooKeeperNet
{
    public interface ISaslClient
    {
        /// <summary>
        /// Start an authentication session.
        /// </summary>
        /// <returns>
        /// An initial, possibly empty, initial response to send to
        /// the server.
        /// </returns>
        byte[] Start(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint);

        /// <summary>
        /// Determines whether the exchange has completed.
        /// </summary>
        /// <value>Whether the exchange has completed.</value>
        bool IsCompleted { get; }

        /// <summary>
        /// Determines whether authentication using this client or
        /// mechanism requires the emission of a "last packet," as
        /// defined by ZooKeeper:
        ///
        /// "GSSAPI: server sends a final packet after authentication
        /// succeeds or fails."
        /// "non-GSSAPI: no final packet from server."
        ///
        /// https://github.com/apache/zookeeper/blob/11c07921c15e/zookeeper-server/src/main/java/org/apache/zookeeper/client/ZooKeeperSaslClient.java#L285-L293
        /// </summary>
        /// <value>Whether a "last packet" is required.</value>
        bool HasLastPacket { get; }

        /// <summary>
        /// Evaluates the challenge data and generate a response.
        /// </summary>
        /// <param name="challenge">The challenge sent from the server.</param>
        /// <returns>The response to send to the server.</returns>
        byte[] EvaluateChallenge(byte[] challenge);

        /// <summary>
        /// Marks authentication as complete, allowing the client to
        /// release resources which won't be needed until the next
        /// <see cref="Start"/>.
        /// </summary>
        void Finish();
    }
}
