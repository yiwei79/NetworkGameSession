using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class UDPTest : MonoBehaviour
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
        // TO DO 1: Create and bind the UDP socket
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 9050);
        serverSocket.Bind(endPoint);
        Debug.Log("UDP Server is running on port 9050");

        // Start the client after server is ready
        m_clientThread.Start();

        // TO DO 3: Receive messages from any client
        IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
        EndPoint Remote = (EndPoint)(sender);

        byte[] buffer = new byte[1024];
        while (isRunning)
        {
            int bytesRead = serverSocket.ReceiveFrom(buffer, ref Remote);

            if (bytesRead == 0)
            {
                Debug.Log("No data received");
                continue;
            }

            string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log("Server received message: " + message);

            // TO DO 4: Send "ping" back to the client
            string response = "ping";
            byte[] responseBuffer = System.Text.Encoding.UTF8.GetBytes(response);
            serverSocket.SendTo(responseBuffer, responseBuffer.Length, SocketFlags.None, Remote);
            Debug.Log("Server sent ping back to client");

            Thread.Sleep(1000);
        }
    }

    void ClientProcess()
    {
        Thread.Sleep(1000); // Wait for server to start

        // TO DO 2: Create UDP socket and server endpoint
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);

        try
        {
            Debug.Log("UDP Client ready to send messages");

            byte[] buffer = new byte[1024];
            int messageCount = 0;

            while (isRunning)
            {
                messageCount++;
                // TO DO 2.1: Send message to server using SendTo
                string messageToSend = "Message from client: " + messageCount;
                byte[] messageBuffer = System.Text.Encoding.UTF8.GetBytes(messageToSend);
                clientSocket.SendTo(messageBuffer, messageBuffer.Length, SocketFlags.None, serverEndPoint);
                Debug.Log("Client sent: " + messageToSend);

                // TO DO 5: Receive the ping from server
                EndPoint Remote = (EndPoint)serverEndPoint;
                int bytesRead = clientSocket.ReceiveFrom(buffer, ref Remote);

                if (bytesRead > 0)
                {
                    string receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Debug.Log("Client received: " + receivedMessage);
                }

                Thread.Sleep(1000);
            }
        }
        catch (SocketException e)
        {
            Debug.LogError($"UDP Client error: {e.Message}");
        }
        finally
        {
            clientSocket.Close();
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;

        if (serverSocket != null)
        {
            serverSocket.Close();
        }

        if (m_serverThread != null && m_serverThread.IsAlive)
        {
            m_serverThread.Abort();
        }

        if (m_clientThread != null && m_clientThread.IsAlive)
        {
            m_clientThread.Abort();
        }
    }
}
