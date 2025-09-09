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

        // 所有的都需要更新一下他们的list
        // foreach (var cs in MainClass.clients.Values)
        {
            MainClass.Send(clientState, sendStr);
        }
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

    public static void MsgAttack(ClientState clientState, string msg)
    {
        var sendStr = "Attack|" + msg;
        foreach (var cs in MainClass.clients.Values)
        {
            MainClass.Send(cs, sendStr);
        }
    }

    public static void MsgHit(ClientState clientState, string msg)
    {
        var split = msg.Split(',');
        var attDesc = split[0]; // 攻击者
        var hitDesc =  split[1]; // 被攻击者

        ClientState hitCS = null;

        foreach (var cs in MainClass.clients.Values)
        {
            if (cs.Socket.RemoteEndPoint.ToString() == hitDesc)
            {
                hitCS = cs; // 找出被攻击的角色
            }
        }

        if (hitCS == null)
        {
            return;
        }
        
        hitCS.hp -= 25; // 血量没有体现在客户端上 在服务端上的数据
        
        var sendStr = "Hit|" + msg;
        foreach (var cs in MainClass.clients.Values)
        {
            MainClass.Send(cs, sendStr); // 直接将消息广播出去
        }
        
        if (hitCS.hp <= 0)
        {
            // 角色死亡协议
            sendStr = "Die|" + hitCS.Socket.RemoteEndPoint;
            foreach (var cs in MainClass.clients.Values)
            {
                MainClass.Send(cs, sendStr);
            }
        }
    }
}