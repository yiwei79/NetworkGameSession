using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class TCPTest : MonoBehaviour
{
    Thread m_serverThread;
    Thread m_clientThread;

    private Socket serverSocket;
    private bool isRunning = true;

    void Start()
    {
      m_clientThread = new Thread(ClientProcess);
      m_serverThread = new Thread(ServerProcess);

      m_serverThread.Start();
      
    }

    void ServerProcess()
    {
      serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
      IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback,9050);
      serverSocket.Bind(endPoint);
      serverSocket.Listen(10);
      Debug.Log("Server is running on port 9050");

      m_clientThread.Start();
      Socket clientHandler = serverSocket.Accept();
      Debug.Log("Client accepted");

      //Recieve message from client
      byte[] buffer = new byte[1024];
      while(isRunning)
      {
        int bytesRead = clientHandler.Receive(buffer);

        if(bytesRead == 0)
        {
            Debug.Log("Client disconnected");
            break;
        }

        string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
        Debug.Log("Recived message from client: " + message);

        // Send Response back to client
        string response = "Message received: " + message;
        byte[] responseBuffer = System.Text.Encoding.UTF8.GetBytes(response);
        clientHandler.Send(responseBuffer);
        Debug.Log("Server say Hello Back to Client");

        Thread.Sleep(1000);
      }
      
    //   clientHandler.Close();
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
            

            byte[] buffer = new byte[1024];
            int messageCount = 0;
            while(isRunning)
            {
                messageCount++;
                string messageToSend = "Message from client: " + messageCount;
                byte[] messageBuffer = System.Text.Encoding.UTF8.GetBytes(messageToSend);
                clientSocket.Send(messageBuffer);
                Debug.Log("Client sent message: " + messageToSend);

                //Receive message from server
                int bytesRead = clientSocket.Receive(buffer);

                if(bytesRead == 0)
                {
                    Debug.Log("Server disconnected");
                    break;
                }
                string receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Debug.Log("Server sent message: " + receivedMessage);

                Thread.Sleep(1000);
            }
            
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
        isRunning = false;

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
