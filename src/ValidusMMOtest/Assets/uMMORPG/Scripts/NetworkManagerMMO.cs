// We use a custom NetworkManager that also takes care of login, character
// selection, character creation and more.
//
// We don't use the playerPrefab, instead all available player classes should be
// dragged into the spawnable objects property.
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

public class NetworkManagerMMO : NetworkManager {
    // <conn, account> dict for the lobby
    // (people that are still creating or selecting characters)
    Dictionary<NetworkConnection, string> lobby = new Dictionary<NetworkConnection, string>();

    [Header("Credentials")]
    public string id = "";
    public string pw = "";

    // character database
    [Header("Database")] // dont show in inspector to avoid instantiation
    public int charLimit = 4;
    public int charNameMaxLength = 16;
    public int accountMaxLength = 16;
    [SerializeField] float saveInterval = 60f; // in seconds

    void Awake() {
        // handshake packet handlers
        NetworkServer.RegisterHandler(LoginMsg.MsgId, OnServerLogin);
        NetworkServer.RegisterHandler(CharacterCreateMsg.MsgId, OnServerCharacterCreate);
        NetworkServer.RegisterHandler(CharacterDeleteMsg.MsgId, OnServerCharacterDelete);

        // headless mode? then automatically start a dedicated server
        // (because we can't click the button in headless mode)
        if (Utils.IsHeadless()) {
            print("headless mode detected, starting dedicated server");
            StartServer();
        }
    }

    // client popup messages ///////////////////////////////////////////////////
    void ClientSendPopup(NetworkConnection conn, string error, bool causesDisconnect) {
        var msg = new ErrorMsg();
        msg.text = error;
        msg.causesDisconnect = causesDisconnect;
        conn.Send(ErrorMsg.MsgId, msg);
    }

    void OnClientReceivePopup(NetworkMessage netMsg) {
        var msg = netMsg.ReadMessage<ErrorMsg>();
        print("OnClientReceivePopup: " + msg.text);

        // show a popup
        FindObjectOfType<UIPopup>().Show(msg.text);

        // disconnect if it was an important network error
        // (this is needed because the login failure message doesn't disconnect
        //  the client immediately (only after timeout))
        if (msg.causesDisconnect) {
            netMsg.conn.Disconnect();

            // also stop the host if running as host
            // (host shouldn't start server but disconnect client for invalid
            //  login, which would be pointless)
            if (NetworkServer.active) StopHost();
        }
    }

    // start & stop ////////////////////////////////////////////////////////////
    public override void OnStartServer() {
        FindObjectOfType<UILogin>().Hide();

        // invoke saving
        InvokeRepeating("SavePlayers", saveInterval, saveInterval);

        // call base function to guarantee proper functionality
        base.OnStartServer();
    }

    public override void OnStopServer() {
        print("OnStopServer");
        CancelInvoke("SavePlayers");

        // call base function to guarantee proper functionality
        base.OnStopServer();
    }


    // handshake: login ////////////////////////////////////////////////////////
    public bool IsConnecting() {
        return NetworkClient.active && !ClientScene.ready;
    }

    public override void OnClientConnect(NetworkConnection conn) {
        print("OnClientConnect");

        // setup handlers
        // the following workaround was needed to avoid a exception in Unity 5.2
        //var client = NetworkClient.allClients[0]; <- not needed in 5.3 anymore
        client.RegisterHandler(CharactersAvailableMsg.MsgId, OnClientCharactersAvailable);
        client.RegisterHandler(ErrorMsg.MsgId, OnClientReceivePopup);

        // send login packet
        var msg = new LoginMsg();
        msg.id = id;
        msg.pw = pw;
        conn.Send(LoginMsg.MsgId, msg);
        print("login msg was sent");

        // call base function to make sure that client becomes "ready"
        //base.OnClientConnect(conn);
        ClientScene.Ready(conn); // from bitbucket OnClientConnect source
    }

    bool AccountLoggedIn(string account) {
        // in lobby or in world?
        return lobby.ContainsValue(account) ||
               NetworkServer.objects.Any(e => e.Value.GetComponent<Player>() &&
                                              e.Value.GetComponent<Player>().account == account);
    }

    bool IsValidAccount(string id, string pw) {
        // this function can be used to verify account credentials in a database
        // or a content management system. 
        //        
        // for example, we could setup a content management system with a forum,
        // news, shop etc. and then use a simple HTTP-GET to check the account
        // info, for example:
        //
        //   var request = new WWW("example.com/verify.php?id="+id+"&amp;pw="+pw);
        //   while (!request.isDone)
        //       print("loading...");
        //   return request.error == null && request.text == "ok";
        //
        // where verify.php is a script like this one:
        //   <?php
        //   // id and pw set with HTTP-GET?
        //   if (isset($_GET['id']) && isset($_GET['pw'])) {
        //       // validate id and pw by using the CMS, for example in Drupal:
        //       if (user_authenticate($_GET['id'], $_GET['pw']))
        //           echo "ok";
        //       else
        //           echo "invalid id or pw";
        //   }
        //   ?>
        //
        // or we could check in a MYSQL database:
        //   var dbConn = new MySql.Data.MySqlClient.MySqlConnection("Persist Security Info=False;server=localhost;database=notas;uid=root;password=" + dbpwd);
        //   var cmd = dbConn.CreateCommand();
        //   cmd.CommandText = "SELECT id FROM accounts WHERE id='" + id + "' AND pw='" + pw + "'";
        //   dbConn.Open();
        //   var reader = cmd.ExecuteReader();
        //   if (reader.Read())
        //       return reader.ToString() == id;
        //   return false;

        // not empty?
        if (!Utils.IsNullOrWhiteSpace(id) && !Utils.IsNullOrWhiteSpace(pw)) {
            // for now: allow all accounts for easier testing
            return true;
        }
        return false;
    }

    void OnServerLogin(NetworkMessage netMsg) {
        print("OnServerLogin " + netMsg.conn);
        var msg = netMsg.ReadMessage<LoginMsg>();

        // not too long?
        if (msg.id.Length <= accountMaxLength) {
            // only contains letters, number and underscore and not empty (+)?
            // (important for database safety etc.)
            if (Regex.IsMatch(msg.id, @"^[a-zA-Z0-9_]+$")) {
                // validate account info
                if (IsValidAccount(msg.id, msg.pw)) {
                    // not in lobby and not in world yet?
                    if (!AccountLoggedIn(msg.id)) {
                        print("login successful: " + msg.id);

                        // add to logged in accounts
                        lobby[netMsg.conn] = msg.id;

                        // send available characters to client
                        var msgchars = new CharactersAvailableMsg();
                        msgchars.characters = Database.CharactersForAccount(msg.id).ToArray();
                        netMsg.conn.Send(CharactersAvailableMsg.MsgId, msgchars);
                    } else {
                        print("account already logged in: " + msg.id);
                        ClientSendPopup(netMsg.conn, "already logged in", true);

                        // note: we should disconnect the client here, but we can't as
                        // long as unity has no "SendAllAndThenDisconnect" function,
                        // because then the error message would never be sent.
                        //netMsg.conn.Disconnect();
                    }
                } else {
                    print("invalid account or password for: " + msg.id);
                    ClientSendPopup(netMsg.conn, "invalid id or pw", true);
                }
            } else {
                print("account invalid: " + msg.id);
                ClientSendPopup(netMsg.conn, "invalid account", true);
            }
        } else {
            print("account too long: " + msg.id);
            ClientSendPopup(netMsg.conn, "account too long", true);
        }
    }

    // handshake: character selection //////////////////////////////////////////
    void OnClientCharactersAvailable(NetworkMessage netMsg) {
        var msg = netMsg.ReadMessage<CharactersAvailableMsg>();
        print("characters available:" + msg.characters.Length);

        // hide login and creation, show selection
        FindObjectOfType<UILogin>().Hide();
        FindObjectOfType<UICharacterCreation>().Hide();
        FindObjectOfType<UICharacterSelection>().characters = msg.characters;
        FindObjectOfType<UICharacterSelection>().Show();
    }

    // called after the client calls ClientScene.AddPlayer with a msg parameter
    public override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId, NetworkReader extraMsg) {
        print("OnServerAddPlayer extra");
        if (extraMsg != null) {            
            // only while in lobby (aka after handshake and not ingame)
            if (lobby.ContainsKey(conn)) {
                // read the index and find the n-th character
                // (only if we know that he is not ingame, otherwise lobby has
                //  no netMsg.conn key)
                var msg = extraMsg.ReadMessage<CharacterSelectMsg>();
                var account = lobby[conn];
                var chars = Database.CharactersForAccount(account);

                // validate index
                if (0 <= msg.index && msg.index < chars.Count) {
                    print(account + " selected player " + chars[msg.index]);

                    // load character data
                    var go = Database.CharacterLoad(account, chars[msg.index], GetPlayerClasses());

                    // add to client
                    NetworkServer.AddPlayerForConnection(conn, go, playerControllerId);

                    // remove from lobby
                    lobby.Remove(conn);
                } else {
                    print("invalid character index: " + account + " " + msg.index);
                    ClientSendPopup(conn, "invalid character index", false);
                }
            } else {
                print("AddPlayer: not in lobby" + conn);
                ClientSendPopup(conn, "AddPlayer: not in lobby", true);
            }
        } else {
            print("missing extraMessageReader");
            ClientSendPopup(conn, "missing parameter", true);
        }
    }

    // handshake: character creation ///////////////////////////////////////////
    // find all available player classes
    public List<Player> GetPlayerClasses() {
        return (from go in spawnPrefabs
                where go.GetComponent<Player>() != null
                select go.GetComponent<Player>()).ToList();
    }

    void OnServerCharacterCreate(NetworkMessage netMsg) {
        print("OnServerCharacterCreate " + netMsg.conn);
        var msg = netMsg.ReadMessage<CharacterCreateMsg>();

        // can only delete while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(netMsg.conn)) {
            // not too long?
            if (msg.name.Length <= charNameMaxLength) {
                // only contains letters, number and underscore and not empty (+)?
                // (important for database safety etc.)
                if (Regex.IsMatch(msg.name, @"^[a-zA-Z0-9_]+$")) {
                    // not existant yet?
                    var account = lobby[netMsg.conn];
                    if (!Database.CharacterExists(msg.name)) {
                        // not too may characters created yet?
                        if (Database.CharactersForAccount(account).Count < charLimit) {
                            // valid class index?
                            var classes = GetPlayerClasses();
                            if (0 <= msg.classIndex && msg.classIndex < classes.Count) {
                                // create new character based on the class
                                // prefab's defaults
                                print("creating character: " + msg.name + " " + msg.classIndex);
                                var player = classes[msg.classIndex].GetComponent<Player>();
                                Database.CharacterCreate(account, msg.name, player, GetStartPosition().position);

                                // send available characters list again, causing
                                // the client to switch to the character
                                // selection scene again
                                var msgchars = new CharactersAvailableMsg();
                                msgchars.characters = Database.CharactersForAccount(account).ToArray();
                                netMsg.conn.Send(CharactersAvailableMsg.MsgId, msgchars);
                            } else {
                                print("character invalid class: " + msg.classIndex);
                                ClientSendPopup(netMsg.conn, "character invalid class", false);
                            }
                        } else {
                            print("character limit reached: " + msg.name);
                            ClientSendPopup(netMsg.conn, "character limit reached", false);
                        }
                    } else {
                        print("character name already exists: " + msg.name);
                        ClientSendPopup(netMsg.conn, "name already exists", false);
                    }
                } else {
                    print("character name invalid: " + msg.name);
                    ClientSendPopup(netMsg.conn, "invalid name", false);
                }
            } else {
                print("character name too long: " + msg.name);
                ClientSendPopup(netMsg.conn, "name too long", false);
            }
        } else {
            print("CharacterCreate: not in lobby");
            ClientSendPopup(netMsg.conn, "CharacterCreate: not in lobby", true);
        }
    }

    void OnServerCharacterDelete(NetworkMessage netMsg) {
        print("OnServerCharacterDelete " + netMsg.conn);
        var msg = netMsg.ReadMessage<CharacterDeleteMsg>();

        // can only delete while in lobby (aka after handshake and not ingame)
        if (lobby.ContainsKey(netMsg.conn)) {
            var account = lobby[netMsg.conn];
            var chars = Database.CharactersForAccount(account);

            // validate index
            if (0 <= msg.index && msg.index < chars.Count) {
                // delete the character
                print("delete character: " + msg.index);
                Database.CharacterDelete(account, chars[msg.index]);

                // send the new character list to client
                var msgchars = new CharactersAvailableMsg();
                msgchars.characters = Database.CharactersForAccount(account).ToArray();
                netMsg.conn.Send(CharactersAvailableMsg.MsgId, msgchars);
            } else {
                print("invalid character index: " + account + " " + msg.index);
                ClientSendPopup(netMsg.conn, "invalid character index", false);
            }
        } else {
            print("CharacterDelete: not in lobby: " + netMsg.conn);
            ClientSendPopup(netMsg.conn, "CharacterDelete: not in lobby", true);
        }
    }

    // player saving ///////////////////////////////////////////////////////////
    // we have to save all players at once to make sure that item trading is
    // perfectly save. if we would invoke a save function every few minutes on
    // each player seperately then it could happen that two players trade items
    // and only one of them is saved before a server crash - hence causing item
    // duplicates.
    void SavePlayers() {
        // save all players
        foreach(var entry in NetworkServer.objects) {
            // is this object a player? (not a monster etc.)
            var player = entry.Value.GetComponent<Player>();
            if (player != null) {
                Database.CharacterSave(player);                
                print("saved:" + player.name);
            }
        }
    }

    // stop/disconnect /////////////////////////////////////////////////////////
    // called on the server when a client disconnects
    public override void OnServerDisconnect(NetworkConnection conn) {
        print("OnServerDisconnect " + conn);

        // save player (if any)
        // note: playerControllers.Count cant be used as check because
        // nothing is removed from that list, even after disconnect. It still
        // contains entries like: ID=0 NetworkIdentity NetID=null Player=null
        // (which might be a UNET bug)
        var go = conn.playerControllers.Find(pc => pc.gameObject != null);
        if (go != null) {
            Database.CharacterSave(go.gameObject.GetComponent<Player>());
            print("saved:" + go.gameObject.name);
        } else print("no player to save for: " + conn);

        // remove logged in account after everything else was done
        lobby.Remove(conn); // just returns false if not found

        // do whatever the base function did (destroy the player etc.)
        base.OnServerDisconnect(conn);
    }

    // called on the client if he disconnects
    public override void OnClientDisconnect(NetworkConnection conn) {
        print("OnClientDisconnect");

        // show a popup so that users now what happened
        FindObjectOfType<UIPopup>().Show("Disconnected.");

        // show login mask again
        FindObjectOfType<UILogin>().Show();

        // call base function to guarantee proper functionality
        base.OnClientDisconnect(conn);

        // call StopClient to clean everything up properly (otherwise
        // NetworkClient.active remains false after next login)
        StopClient();
    }

    // called when quitting the application by closing the window / pressing
    // stop in the editor
    // -> we want to send the quit packet to the server instead of waiting for a
    //    timeout
    // -> this also avoids the OnDisconnectError UNET bug (#838689) more often
    void OnApplicationQuit() {
        if (IsClientConnected()) {
            StopClient();
            print("OnApplicationQuit: stopped client");
        }
    }
}
