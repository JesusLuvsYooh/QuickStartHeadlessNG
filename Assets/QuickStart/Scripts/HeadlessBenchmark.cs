﻿using System;
using System.Collections;
using Mirror.KCP;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Mirror.ENet;

namespace Mirror.HeadlessBenchmark
{
    public class HeadlessBenchmark : MonoBehaviour
    {
        public NetworkManager networkManager;
        public ServerObjectManager serverObjectManager;
        public GameObject MonsterPrefab;
        public GameObject PlayerPrefab;
        public string editorArgs;
        public KcpTransport kcpTransport;

        string[] cachedArgs;
        string port;

        void Start()
        {
            cachedArgs = Environment.GetCommandLineArgs();

#if UNITY_EDITOR
            cachedArgs = editorArgs.Split(' ');
#endif

            HeadlessStart();

        }
        private IEnumerator DisplayFramesPerSecons()
        {
            int previousFrameCount = Time.frameCount;
            long previousMessageCount = 0;

            while (true)
            {
                yield return new WaitForSeconds(1);
                int frameCount = Time.frameCount;
                int frames = frameCount - previousFrameCount;

                long messageCount = kcpTransport != null ? kcpTransport.ReceivedMessageCount : 0;
                long messages = messageCount - previousMessageCount;

#if UNITY_EDITOR
                Debug.LogFormat("{0} FPS {1} messages {2} clients", frames, messages, networkManager.server.NumPlayers);
#else
                Console.WriteLine("{0} FPS {1} messages {2} clients", frames, messages, networkManager.server.NumPlayers);
#endif
                previousFrameCount = frameCount;
                previousMessageCount = messageCount;
            }
        }

        void HeadlessStart()
        {
            //Try to find port
            port = GetArgValue("-port");

            GetComponent<HeadlessFrameLimiter>().serverTickRate = int.Parse(GetArgValue("-frameRate"));

            staticC.traffic = int.Parse(GetArgValue("-traffic"));

            //Try to find Transport
            ParseForTransport();

            //Server mode?
            ParseForServerMode();

            //Or client mode?
            StartClients().Forget();

            ParseForHelp();
        }

        void OnServerStarted()
        {
            StartCoroutine(DisplayFramesPerSecons());

            GetComponent<NetworkSceneManager>().ChangeServerScene("MyScene");

            string monster = GetArgValue("-monster");
            if (!string.IsNullOrEmpty(monster))
            {
                for (int i = 0; i < int.Parse(monster); i++)
                    SpawnMonsters(i);
            }
        }

        void SpawnMonsters(int i)
        {
            GameObject monster = Instantiate(MonsterPrefab);
            monster.gameObject.name = $"Monster {i}";
            serverObjectManager.Spawn(monster.gameObject);
        }

        async UniTask StartClient(int i, Transport transport, string networkAddress)
        {
            var clientGo = new GameObject($"Client {i}", typeof(NetworkClient), typeof(ClientObjectManager), typeof(NetworkSceneManager));
            NetworkClient client = clientGo.GetComponent<NetworkClient>();
            clientGo.GetComponent<NetworkSceneManager>().client = client;
            ClientObjectManager objectManager = clientGo.GetComponent<ClientObjectManager>();

            GetComponent<PlayerSpawner>().client = client;

            objectManager.RegisterPrefab(MonsterPrefab.GetComponent<NetworkIdentity>());
            objectManager.RegisterPrefab(PlayerPrefab.GetComponent<NetworkIdentity>());

            objectManager.networkSceneManager = clientGo.GetComponent<NetworkSceneManager>();
            objectManager.client = client;
            objectManager.Start();
            client.Transport = transport;

            try
            {
                await client.ConnectAsync(networkAddress);
                client.Send(new AddPlayerMessage());
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

        }

        void ParseForServerMode()
        {
            if (!string.IsNullOrEmpty(GetArg("-server")))
            {
                networkManager.server.Started.AddListener(OnServerStarted);
                networkManager.server.Authenticated.AddListener(conn => serverObjectManager.SetClientReady(conn));
                _ = networkManager.server.ListenAsync();
                Console.WriteLine("Starting Server Only Mode");
            }
        }

        async UniTaskVoid StartClients()
        {
            string client = GetArg("-client");
            if (!string.IsNullOrEmpty(client))
            {
                //network address provided?
                string address = GetArgValue("-address");
                if (string.IsNullOrEmpty(address))
                {
                    address = "localhost";
                }

                //nested clients
                int clonesCount = 1;
                string clonesString = GetArgValue("-client");
                if (!string.IsNullOrEmpty(clonesString))
                {
                    clonesCount = int.Parse(clonesString);
                }

                Console.WriteLine("Starting {0} clients", clonesCount);

                // connect from a bunch of clients
                for (int i = 0; i < clonesCount; i++)
                {
                    await StartClient(i, networkManager.client.Transport, address);
                    await UniTask.Delay(500);

                    Debug.LogFormat("Started {0} clients", i + 1);
                }
            }
        }

        void ParseForHelp()
        {
            if (!string.IsNullOrEmpty(GetArg("-help")))
            {
                Console.WriteLine("--==MirrorNG HeadlessClients Benchmark==--");
                Console.WriteLine("Please start your standalone application with the -nographics and -batchmode options");
                Console.WriteLine("Also provide these arguments to control the autostart process:");
                Console.WriteLine("-server (will run in server only mode)");
                Console.WriteLine("-client 1234 (will run the specified number of clients)");
                Console.WriteLine("-transport tcp (transport to be used in test. add more by editing HeadlessBenchmark.cs)");
                Console.WriteLine("-address example.com (ip of server to connect to)");
                Console.WriteLine("-port 1234 (port used by transport)");
                Console.WriteLine("-monster 100 (number of monsters to spawn on the server)");
                Console.WriteLine(" - Type 0 for default - ");
                Console.WriteLine("Traffic 0=none   1=light (card game)  2=active (social game)  3=heavy (mmo)   4=frequent (fps)");

                Application.Quit();
            }
        }

        void ParseForTransport()
        {
            string transport = GetArgValue("-transport");

            if (string.IsNullOrEmpty(transport) || transport.Equals("kcp"))
            {
                KcpTransport newTransport = networkManager.gameObject.AddComponent<KcpTransport>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Port = ushort.Parse(port);
                }

                networkManager.server.transport = newTransport;
                networkManager.client.Transport = newTransport;

                newTransport.HashCashBits = 15;
                newTransport.SendWindowSize = 8192; //256
                newTransport.ReceiveWindowSize = 8192;
                newTransport.delayMode = KcpDelayMode.Fast3;

                kcpTransport = newTransport;
            }

            if (transport != null && transport.Equals("enet"))
            {
                IgnoranceNG newTransport = networkManager.gameObject.AddComponent<IgnoranceNG>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Config.CommunicationPort = ushort.Parse(port);
                }

                networkManager.server.transport = newTransport;
                networkManager.client.Transport = newTransport;
            }

            if (transport != null && transport.Equals("libuv2k"))
            {
                KcpTransport newTransport = networkManager.gameObject.AddComponent<KcpTransport>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Port = ushort.Parse(port);
                }

                networkManager.server.transport = newTransport;
                networkManager.client.Transport = newTransport;
            }

            if (transport != null && transport.Equals("litelib"))
            {
                KcpTransport newTransport = networkManager.gameObject.AddComponent<KcpTransport>();

                //Try to apply port if exists and needed by transport.
                if (!string.IsNullOrEmpty(port))
                {
                    newTransport.Port = ushort.Parse(port);
                }

                networkManager.server.transport = newTransport;
                networkManager.client.Transport = newTransport;
            }

        }

        string GetArgValue(string name)
        {
            for (int i = 0; i < cachedArgs.Length; i++)
            {
                if (cachedArgs[i] == name && cachedArgs.Length > i + 1)
                {
                    return cachedArgs[i + 1];
                }
            }
            return null;
        }

        string GetArg(string name)
        {
            for (int i = 0; i < cachedArgs.Length; i++)
            {
                if (cachedArgs[i] == name)
                {
                    return cachedArgs[i];
                }
            }
            return null;
        }
    }
}
