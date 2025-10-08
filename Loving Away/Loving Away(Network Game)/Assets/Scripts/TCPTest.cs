using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class TCPTest : MonoBehaviour
{
    Thread m_serverThread;
    Thread m_clientThread;

    private Socket serverSocket;

    void Start()
    {
      m_clientThread = new Thread(ClientProcess);
      m_serverThread = new Thread(ServerProcess);

      m_serverThread.Start();
      m_clientThread.Start();
    }

    void ServerProcess()
    {
      serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback,9050);
      serverSocket.Bind(endPoint);
      serverSocket.Listen(10);
      Debug.Log("Server is running on port 9050");
      
      Socket clientHandler = serverSocket.Accept();
      Debug.Log("Client accepted");

      //Recieve message from client
      byte[] buffer = new byte[1024];
      int bytesRead = clientHandler.Receive(buffer);
      string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
      Debug.Log("Client sent message: " + message);

      //Send message to client
      string response = "Message received";
      byte[] responseBuffer = System.Text.Encoding.UTF8.GetBytes(response);
      clientHandler.Send(responseBuffer);
      Debug.Log("Server say Hello Back to Client");

      clientHandler.Close();
    }

    void ClientProcess()
    {
        Thread.Sleep(1000);
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback,9050);
        try
        {
            // Connect to the server
            clientSocket.Connect(serverEndPoint);
            Debug.Log("Client connected to server!");
            
            // Send a message
            byte[] message = System.Text.Encoding.UTF8.GetBytes("Hello from client!");
            clientSocket.Send(message);
            Debug.Log("Client sent message");

            //Recieve message from server
            byte[] buffer = new byte[1024];
            int bytesRead = clientSocket.Receive(buffer);
            string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log("Server sent message: " + message);
            
        }
        catch (SocketException e)
        {
            Debug.LogError($"Client connection failed: {e.Message}");
        }
        finally
        {
            clientSocket.Close();
        }
    }

    void OnApplicationQuit()
    {
        if(serverSocket != null)
        {
            serverSocket.Close();
        }
        
        if(m_serverThread != null && m_serverThread.IsAlive)
        {
            m_serverThread.Abort();
        }
        
        if(m_clientThread != null && m_clientThread.IsAlive)
        {
            m_clientThread.Abort();
        }
    }
}
