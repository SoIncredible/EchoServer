namespace EchorServer;

public class EventHandler
{
    public static void OnDisconnect(ClientState clientState)
    {
        var desc = clientState.Socket.RemoteEndPoint.ToString();
        var sendStr = "Leave|" + desc;
        foreach (var cs in MainClass.clients.Values)
        {
            MainClass.Send(cs, sendStr);
        }
    }
}