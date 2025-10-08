using UnityEngine;
using System.Net;
using System.Net.Sockets;

public class TCPTest : MonoBehaviour
{
    Thread m_serverThread;
    Thread m_clientThread;

    private Socket serverSocket;

    void Start()
    {
      ServerProcess();

      m_clientThread = new Thread(ClientProcess);
      m_serverThread = new Thread(ServerProcess);
    }

    void ServerProcess()
    {
      serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback,9050);
      serverSocket.Bind(endPoint);
      serverSocket.Listen(10);
      Debug.Log("Server is running on port 9050");

      m_clientThread.Start();
    }

    void OnApplicationQuit()
    {
        if(serverSocket != null)
        {
            serverSocket.Close();
        }
    }

}
