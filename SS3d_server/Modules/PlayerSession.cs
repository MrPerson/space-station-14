﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Lidgren.Network;
using SS3D_Server.Atom;
using SS3D_Server;
using SS3D_shared;
using SS3D_shared.GO;
using SGO;
using ServerServices;

namespace SS3D_Server.Modules
{
    public class PlayerSession
    {
        /* This class represents a connected player session */

        public NetConnection connectedClient;
        public Atom.Atom attachedAtom;
        public string name = "";
        public SessionStatus status;
        public JobDefinition assignedJob;

        public PlayerSession(NetConnection client)
        {         
            if (client != null)
            {
                connectedClient = client;
                OnConnect();
            }
            else
                status = SessionStatus.Zombie;
        }

        public void AttachToAtom(Atom.Atom a)
        {
            DetachFromAtom();
            a.attachedClient = connectedClient;
            //Add input component.
            a.AddComponent(ComponentFamily.Input, SGO.ComponentFactory.Singleton.GetComponent("KeyBindingInputComponent"));
            a.AddComponent(ComponentFamily.Mover, ComponentFactory.Singleton.GetComponent("PlayerInputMoverComponent"));
            a.AddComponent(ComponentFamily.Actor, ComponentFactory.Singleton.GetComponent("BasicActorComponent"));
            attachedAtom = a;
            SendAttachMessage();
        }

        public void DetachFromAtom()
        {
            if (attachedAtom != null)
            {
                attachedAtom.attachedClient = null;
                attachedAtom.Die();
                attachedAtom.RemoveComponent(ComponentFamily.Input);
                attachedAtom.RemoveComponent(ComponentFamily.Mover);
                attachedAtom.RemoveComponent(ComponentFamily.Actor);
                attachedAtom = null;
            }
        }

        private void SendAttachMessage()
        {
            NetOutgoingMessage m = SS3DNetServer.Singleton.CreateMessage();
            m.Write((byte)NetMessage.PlayerSessionMessage);
            m.Write((byte)PlayerSessionMessage.AttachToAtom);
            m.Write(attachedAtom.Uid);
            SS3DNetServer.Singleton.SendMessage(m, connectedClient);
        }

        public void HandleNetworkMessage(NetIncomingMessage message)
        {
            PlayerSessionMessage messageType = (PlayerSessionMessage)message.ReadByte();
            switch (messageType)
            {
                case PlayerSessionMessage.Verb:
                    HandleVerb(message);
                    break;
                case PlayerSessionMessage.JoinLobby:
                    JoinLobby();
                    break;
                default:
                    break;
            }
        }

        private void HandleVerb(NetIncomingMessage message)
        {
            DispatchVerb(message.ReadString(), message.ReadInt32());
        }

        public void DispatchVerb(string verb, int uid)
        {
            //Handle global verbs
            LogManager.Log("Verb: " + verb + " from " + uid, LogLevel.Debug);
            
            if (uid == 0)
            {
                switch (verb)
                {
                    case "joingame":
                        JoinGame();
                        break;
                    case "toxins":
                        //Need debugging function to add more gas
                    case "save":
                        SS3DServer.Singleton.atomManager.SaveAtoms();
                        SS3DServer.Singleton.map.SaveMap();
                        break;
                    default:
                        break;
                }
            }
            else
            {
                var targetAtom = SS3DServer.Singleton.atomManager.GetAtom(uid);
                targetAtom.HandleVerb(verb);
            }
        }



        public void SetName(string _name)
        {
            name = _name;
            LogManager.Log("Player set name: " + connectedClient.RemoteEndpoint.Address.ToString() + " -> " + name);
            if (attachedAtom != null)
            {
                attachedAtom.SetName(_name);
            }
        }

        public void JoinLobby()
        {
            NetOutgoingMessage m = SS3DNetServer.Singleton.CreateMessage();
            m.Write((byte)NetMessage.PlayerSessionMessage);
            m.Write((byte)PlayerSessionMessage.JoinLobby);
            SS3DNetServer.Singleton.SendMessage(m, connectedClient);
            status = SessionStatus.InLobby;
        }

        public void JoinGame()
        {
            if (connectedClient != null && status != SessionStatus.InGame && SS3DServer.Singleton.runlevel == SS3DServer.RunLevel.Game)
            {
                NetOutgoingMessage m = SS3DNetServer.Singleton.CreateMessage();
                m.Write((byte)NetMessage.JoinGame);
                SS3DNetServer.Singleton.SendMessage(m, connectedClient);

                status = SessionStatus.InGame;
            }
        }

        public void OnConnect()
        {
            status = SessionStatus.Connected;
            //Put player in lobby immediately.
            LogManager.Log("Player connected - " + connectedClient.RemoteEndpoint.Address.ToString());
            JoinLobby();
        }

        public void OnDisconnect()
        {
            status = SessionStatus.Disconnected;
            DetachFromAtom();
        }

        public NetOutgoingMessage CreateGuiMessage(GuiComponentType gui)
        {
            NetOutgoingMessage m = SS3DNetServer.Singleton.CreateMessage();
            m.Write((byte)NetMessage.PlayerUiMessage);
            m.Write((byte)UiManagerMessage.ComponentMessage);
            m.Write((byte)gui);
            return m;
        }
    }
}
