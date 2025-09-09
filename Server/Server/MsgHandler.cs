using EchorServer;

public class MsgHandler
{
    public static void MsgEnter(ClientState clientState, string msg)
    {
        var split = msg.Split(',');
        var desc = split[0];
        var x =  float.Parse(split[1]);
        var y =  float.Parse(split[2]);
        var z =  float.Parse(split[3]);
        var eulY =  float.Parse(split[4]);

        clientState.hp = 100;
        clientState.x = x;
        clientState.y = y;
        clientState.z = z;
        clientState.eulY = eulY;
        var sendStr = "Enter|" + msg;
        foreach (var cs in MainClass.clients.Values)
        {
            MainClass.Send(cs, sendStr);
        }
    }

    public static void MsgList(ClientState clientState, string msg)
    {
        var sendStr = "List|";
        foreach (var cs in MainClass.clients.Values)
        {
            sendStr += cs.Socket.RemoteEndPoint + ",";
            sendStr += cs.x + ",";
            sendStr += cs.y + ",";
            sendStr += cs.z + ",";
            sendStr += cs.eulY + ",";
            sendStr += cs.hp + ",";
        }
        MainClass.Send(clientState, sendStr);
    }

    public static void MsgMove(ClientState clientState, string msg)
    {
        var split = msg.Split(',');
        var desc = split[0];
        var x =  float.Parse(split[1]);
        var y =  float.Parse(split[2]);
        var z =  float.Parse(split[3]);
        clientState.x = x;
        clientState.y = y;
        clientState.z = z;
        var sendStr = "Move|" + msg;
        foreach (var cs in MainClass.clients.Values)
        {
            MainClass.Send(cs, sendStr);
        }
    }
}