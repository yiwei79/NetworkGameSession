using UnityEngine;

/// <summary>
/// Simple connection UI for easy multiplayer playtesting
/// Allows user to choose between hosting a server or connecting as a client
/// Displays at game start, then hides once connection is established
/// </summary>
public class ConnectionUI : MonoBehaviour
{
    public GameNetworkManager networkManager;

    private bool showConnectionUI = true;
    private string serverIP = "127.0.0.1";
    private string serverPort = "9050";

    void OnGUI()
    {
        if (!showConnectionUI) return;

        // Center connection window
        float windowWidth = 400;
        float windowHeight = 250;
        float windowX = (Screen.width - windowWidth) / 2;
        float windowY = (Screen.height - windowHeight) / 2;

        GUI.Window(0, new Rect(windowX, windowY, windowWidth, windowHeight), DrawConnectionWindow, "Loving Away - Network Game");
    }

    void DrawConnectionWindow(int windowID)
    {
        GUILayout.BeginVertical();

        GUILayout.Space(10);
        GUILayout.Label("Choose connection mode:", GUILayout.Height(30));
        GUILayout.Space(10);

        // Server button
        if (GUILayout.Button("Host Server", GUILayout.Height(50)))
        {
            networkManager.isServer = true;
            showConnectionUI = false;
            UnityEngine.Debug.Log("[ConnectionUI] Starting as SERVER");
        }

        GUILayout.Space(20);

        // Client connection
        GUILayout.Label("Connect to Server:");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Server IP:", GUILayout.Width(80));
        serverIP = GUILayout.TextField(serverIP, GUILayout.Width(150));
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Port:", GUILayout.Width(80));
        serverPort = GUILayout.TextField(serverPort, GUILayout.Width(150));
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (GUILayout.Button("Join as Client", GUILayout.Height(50)))
        {
            networkManager.isServer = false;
            networkManager.serverAddress = serverIP;

            if (int.TryParse(serverPort, out int port))
            {
                networkManager.serverPort = port;
            }

            showConnectionUI = false;
            UnityEngine.Debug.Log($"[ConnectionUI] Connecting to {serverIP}:{serverPort}");
        }

        GUILayout.EndVertical();
    }

    /// <summary>
    /// Call this method to show the connection UI again (e.g., after disconnect)
    /// </summary>
    public void ShowUI()
    {
        showConnectionUI = true;
    }

    /// <summary>
    /// Call this method to hide the connection UI
    /// </summary>
    public void HideUI()
    {
        showConnectionUI = false;
    }
}
