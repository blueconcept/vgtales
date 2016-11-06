// Contains all the network messages that we need.
using UnityEngine;
using UnityEngine.Networking;

// client to server ////////////////////////////////////////////////////////////
class LoginMsg : MessageBase {
    public static short MsgId = 1000;
    public string id;
    public string pw;
}

class CharacterSelectMsg : MessageBase {
    public static short MsgId = 1001;
    public int index;
}

class CharacterDeleteMsg : MessageBase {
    public static short MsgId = 1002;
    public int index;
}

class CharacterCreateMsg : MessageBase {
    public static short MsgId = 1003;
    public string name;
    public int classIndex;
}

// server to client ////////////////////////////////////////////////////////////
class ErrorMsg : MessageBase {
    public static short MsgId = 2000;
    public string text;
    public bool causesDisconnect;
}

class CharactersAvailableMsg : MessageBase {
    public static short MsgId = 2001;
    public string[] characters;
}

class ChatWhisperFromMsg : MessageBase {
    public static short MsgId = 2002;
    public string sender;
    public string text;
}

class ChatWhisperToMsg : MessageBase {
    public static short MsgId = 2003;
    public string receiver;
    public string text;
}

class ChatInfoMsg : MessageBase {
    public static short MsgId = 2004;
    public string text;
}