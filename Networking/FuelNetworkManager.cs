using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ScheduleOne.Vehicles;
using ScheduleOne.Networking;
using S1FuelMod.Systems;
using S1FuelMod.Utils;
using ScheduleOne.DevUtilities;
using UnityEngine.Events;
#if MONO
using Steamworks;
#else
using Il2CppSteamworks;
#endif

namespace S1FuelMod.Networking
{
    /// <summary>
    /// Minimal Steam P2P networking manager dedicated to syncing vehicle fuel state for S1FuelMod.
    /// Host rebroadcasts authoritative updates to all clients. Driver sends changes to host.
    /// </summary>
    internal class FuelNetworkManager : IDisposable
    {
        private const int P2P_CHANNEL = 98; // use a high, mod-dedicated channel

        private bool _initialized;
        private bool _disposed;

        // IL2CPP-safe Steam callbacks
#if MONO
        private Callback<P2PSessionRequest_t>? _sessionRequestCb;
        private Callback<P2PSessionConnectFail_t>? _sessionFailCb;
#endif

        // Per-vehicle throttling of outbound updates
        // Removed old throttling system - now using heartbeat-based updates

        // Track registration for fuel systems
        private readonly Dictionary<string, VehicleFuelSystem> _trackedFuelSystems = new Dictionary<string, VehicleFuelSystem>();

        // Heartbeat system for periodic updates
        private float _lastHeartbeatTime = 0f;
        private const float HEARTBEAT_INTERVAL = 3f; // Send updates every 3 seconds
        
        // Track last sent values to avoid sending unnecessary updates
        private readonly Dictionary<string, float> _lastSentLevel = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _lastSentTime = new Dictionary<string, float>();
        
        // Echo suppression: remember last network-received values
        private readonly Dictionary<string, float> _lastNetLevel = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _lastNetTime = new Dictionary<string, float>();

        // One-shot flag to request snapshot after scene/lobby ready when this client is not host
        private bool _snapshotRequested;

        internal void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Check Steam first
                if (!SteamAPI.IsSteamRunning())
                {
                    ModLogger.Warning("FuelNetwork: Steam not running, deferring init");
                    return;
                }

                if (!SteamManager.Initialized)
                {
                    ModLogger.Warning("FuelNetwork: SteamManager not initialized, deferring init");
                    return;
                }

                // Steam callbacks for P2P sessions
                _sessionRequestCb = Callback<P2PSessionRequest_t>.Create(OnSessionRequest);
                _sessionFailCb = Callback<P2PSessionConnectFail_t>.Create(OnSessionConnectFail);

                // Allow relay for reliability
#if !MONO
                SteamNetworking.AllowP2PPacketRelay(true);
#endif

                _initialized = true;
                ModLogger.Info("FuelNetwork: Initialized P2P callbacks");
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: Initialize failed", ex);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                _sessionRequestCb?.Dispose();
                _sessionFailCb?.Dispose();
                _trackedFuelSystems.Clear();
                // Old throttling dictionaries removed
                // Old throttling dictionaries removed
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: Dispose error", ex);
            }
            _disposed = true;
        }

        internal void Update()
        {
            // Try to initialize if not done yet
            if (!_initialized)
            {
                Initialize();
                return;
            }

            // Ensure Steam callbacks process (critical for IL2CPP)
#if !MONO
            try { SteamAPI.RunCallbacks(); } catch { }
#endif
            ProcessIncomingPackets();
            ProcessHeartbeat();
            TryRequestSnapshotOnce();
        }

        private void TryRequestSnapshotOnce()
        {
            if (_snapshotRequested) return;
            var lobby = Singleton<Lobby>.Instance;
            if (lobby == null || !lobby.IsInLobby) 
            {
                // Reset flag if we're not in lobby so we can try again when we join
                _snapshotRequested = false;
                return;
            }
            if (lobby.IsHost) { _snapshotRequested = true; return; }

            // Send a snapshot request to host once
            try
            {
                var msg = new FuelSnapshotRequestMessage();
                SendTo(GetHostId(), msg);
                _snapshotRequested = true;
                ModLogger.FuelDebug("FuelNetwork: Snapshot request sent to host");
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: Failed to request snapshot", ex);
            }
        }

        internal void RegisterFuelSystem(VehicleFuelSystem fuelSystem)
        {
            if (fuelSystem == null) return;
            var networkId = fuelSystem.NetworkID;
            if (string.IsNullOrEmpty(networkId)) return;

            if (_trackedFuelSystems.ContainsKey(networkId)) return;
            _trackedFuelSystems[networkId] = fuelSystem;
            
            ModLogger.Info($"FuelNetwork: Registered fuel system for vehicle NetworkID:{networkId} (GUID:{fuelSystem.VehicleGUID?.Substring(0, 8)}...)");
        }

        internal void UnregisterFuelSystem(VehicleFuelSystem fuelSystem)
        {
            if (fuelSystem == null) return;
            var networkId = fuelSystem.NetworkID;
            if (string.IsNullOrEmpty(networkId)) return;

            _trackedFuelSystems.Remove(networkId);
            _lastSentLevel.Remove(networkId);
            _lastSentTime.Remove(networkId);
            _lastNetLevel.Remove(networkId);
            _lastNetTime.Remove(networkId);
        }

        private void ProcessHeartbeat()
        {
            float now = Time.time;
            if (now - _lastHeartbeatTime < HEARTBEAT_INTERVAL) return;
            
            _lastHeartbeatTime = now;

            var lobby = Singleton<Lobby>.Instance;
            if (lobby == null || !lobby.IsInLobby || lobby.PlayerCount <= 1) return;

            bool isHost = IsHost();
            int updatesSent = 0;

            foreach (var kv in _trackedFuelSystems)
            {
                var networkId = kv.Key;
                var fs = kv.Value;
                if (fs == null) continue;

                float currentLevel = fs.CurrentFuelLevel;

                // Check if we need to send an update
                bool shouldSend = false;

                // Always send if we've never sent for this vehicle
                if (!_lastSentLevel.ContainsKey(networkId))
                {
                    shouldSend = true;
                }
                else
                {
                    float lastSent = _lastSentLevel[networkId];
                    float deltaLevel = Mathf.Abs(currentLevel - lastSent);
                    
                    // Send if significant change (>0.5L difference)
                    if (deltaLevel > 0.1f)
                    {
                        shouldSend = true;
                    }
                }

                if (shouldSend)
                {
                    if (isHost)
                    {
                        BroadcastFuelUpdate(fs);
                    }
                    else
                    {
                        SendTo(GetHostId(), new FuelUpdateMessage
                        {
                            VehicleGuid = networkId,
                            FuelLevel = currentLevel,
                            MaxCapacity = fs.MaxFuelCapacity,
                        });
                    }
                    
                    _lastSentLevel[networkId] = currentLevel;
                    _lastSentTime[networkId] = now;
                    updatesSent++;
                }
            }

            if (updatesSent > 0)
            {
                ModLogger.Info($"FuelNetwork: Heartbeat sent {updatesSent} fuel updates (IsHost: {isHost})");
            }
        }

        // Old throttling methods removed - now using heartbeat-based updates

        private void BroadcastFuelUpdate(VehicleFuelSystem fs)
        {
            try
            {
                var lobby = Singleton<Lobby>.Instance;
                if (lobby == null || !lobby.IsInLobby)
                    return;

                var msg = new FuelUpdateMessage
                {
                    VehicleGuid = fs.NetworkID,  // Use NetworkID instead of GUID
                    FuelLevel = fs.CurrentFuelLevel,
                    MaxCapacity = fs.MaxFuelCapacity,
                };

                // Host broadcasts authoritative update
                Broadcast(msg);
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: BroadcastFuelUpdate failed", ex);
            }
        }

        private void ProcessIncomingPackets()
        {
            try
            {
                uint packetSize;
                CSteamID remoteId;

                int[] channels = new int[] { P2P_CHANNEL, 0, 1, 2 };
                foreach (var channel in channels)
                {
                    while (SteamNetworking.IsP2PPacketAvailable(out packetSize, channel))
                    {
                        ModLogger.Debug($"FuelNetwork: Found P2P packet - size: {packetSize}, channel: {channel}");
                        
                        if (packetSize == 0 || packetSize > 32 * 1024)
                        {
                            // discard oversized
                            var discard = new byte[packetSize];
                            uint read;
                            SteamNetworking.ReadP2PPacket(discard, packetSize, out read, out remoteId, channel);
                            ModLogger.Warning($"FuelNetwork: Discarded oversized packet: {packetSize} bytes");
                            continue;
                        }

                        var data = new byte[packetSize];
                        uint bytesRead;
                        if (SteamNetworking.ReadP2PPacket(data, packetSize, out bytesRead, out remoteId, channel))
                        {
                            ModLogger.Debug($"FuelNetwork: Read P2P packet - {bytesRead} bytes from {remoteId}, channel {channel}");
                            if (bytesRead > 0)
                            {
                                HandlePacket(remoteId, data);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: ProcessIncomingPackets error", ex);
            }
        }

        private void HandlePacket(CSteamID sender, byte[] data)
        {
            ModLogger.Debug($"FuelNetwork: HandlePacket from {sender.m_SteamID} - {data.Length} bytes");
            
            if (!MiniMessageSerializer.IsValidMessage(data))
            {
                ModLogger.Warning($"FuelNetwork: Invalid message format from {sender.m_SteamID}");
                return;
            }

            string? type = MiniMessageSerializer.GetMessageType(data);
            if (string.IsNullOrEmpty(type)) 
            {
                ModLogger.Warning($"FuelNetwork: Empty message type from {sender.m_SteamID}");
                return;
            }

            ModLogger.Debug($"FuelNetwork: Processing {type} from {sender.m_SteamID}");

            if (type == FuelUpdateMessage.TYPE)
            {
                var msg = MiniMessageSerializer.CreateMessage<FuelUpdateMessage>(data);
                OnFuelUpdateReceived(sender, msg);
            }
            else if (type == FuelSnapshotMessage.TYPE)
            {
                var msg = MiniMessageSerializer.CreateMessage<FuelSnapshotMessage>(data);
                OnFuelSnapshotReceived(sender, msg);
            }
            else if (type == FuelSnapshotRequestMessage.TYPE)
            {
                var msg = MiniMessageSerializer.CreateMessage<FuelSnapshotRequestMessage>(data);
                OnFuelSnapshotRequestReceived(sender, msg);
            }
            else
            {
                ModLogger.Warning($"FuelNetwork: Unknown message type: {type}");
            }
        }

        private void OnFuelUpdateReceived(CSteamID sender, FuelUpdateMessage msg)
        {
            bool isHost = IsHost();
            ModLogger.Debug($"FuelNetwork: Received fuel update from {sender.m_SteamID} - Vehicle NetworkID: {msg.VehicleGuid}, Level: {msg.FuelLevel:F2}L, IsHost: {isHost}");
            
            try
            {
                // Apply update locally using NetworkID
                if (_trackedFuelSystems.TryGetValue(msg.VehicleGuid, out var fs) && fs != null)
                {
                    ModLogger.Debug($"FuelNetwork: Applying fuel update to vehicle NetworkID:{msg.VehicleGuid} - {msg.FuelLevel:F2}L");
                    fs.SetMaxCapacity(Mathf.Max(1f, msg.MaxCapacity));
                    fs.SetFuelLevel(Mathf.Clamp(msg.FuelLevel, 0f, fs.MaxFuelCapacity));
                    
                    // Update tracking to prevent echo loops
                    _lastNetLevel[msg.VehicleGuid] = fs.CurrentFuelLevel;
                    _lastNetTime[msg.VehicleGuid] = Time.time;
                    _lastSentLevel[msg.VehicleGuid] = fs.CurrentFuelLevel; // Also update last sent to prevent immediate re-send
                }
                else
                {
                    if (_trackedFuelSystems.Count > 0)
                    {
                        ModLogger.Warning($"FuelNetwork: Vehicle NetworkID:{msg.VehicleGuid} not found in tracked systems (have {_trackedFuelSystems.Count} tracked)");
                    }
                }

                // Host rebroadcasts to everyone (but not back to sender)
                if (isHost)
                {
                    ModLogger.Debug($"FuelNetwork: Host rebroadcasting fuel update for NetworkID:{msg.VehicleGuid}");
                    BroadcastToOthers(msg, sender);
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: OnFuelUpdateReceived error", ex);
            }
        }

        private void OnFuelSnapshotRequestReceived(CSteamID sender, FuelSnapshotRequestMessage _)
        {
            if (!IsHost()) return;

            try
            {
                var payload = new List<FuelUpdateMessage.Item>();
                foreach (var kv in _trackedFuelSystems)
                {
                    var fs = kv.Value;
                    if (fs == null) continue;
                    payload.Add(new FuelUpdateMessage.Item
                    {
                        VehicleGuid = fs.NetworkID,  // Use NetworkID instead of GUID
                        FuelLevel = fs.CurrentFuelLevel,
                        MaxCapacity = fs.MaxFuelCapacity
                    });
                }

                var snap = new FuelSnapshotMessage { Items = payload.ToArray() };
                SendTo(sender, snap);
                ModLogger.Debug($"FuelNetwork: Sent snapshot with {payload.Count} items to {sender.m_SteamID}");
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: OnFuelSnapshotRequestReceived error", ex);
            }
        }

        private void OnFuelSnapshotReceived(CSteamID sender, FuelSnapshotMessage msg)
        {
            try
            {
                if (msg.Items == null) return;
                foreach (var it in msg.Items)
                {
                    if (string.IsNullOrEmpty(it.VehicleGuid)) continue;
                    if (_trackedFuelSystems.TryGetValue(it.VehicleGuid, out var fs) && fs != null)
                    {
                        fs.SetMaxCapacity(Mathf.Max(1f, it.MaxCapacity));
                        fs.SetFuelLevel(Mathf.Clamp(it.FuelLevel, 0f, fs.MaxFuelCapacity));
                        // Update echo suppression for NetworkID
                        _lastNetLevel[it.VehicleGuid] = fs.CurrentFuelLevel;
                        _lastNetTime[it.VehicleGuid] = Time.time;
                    }
                    else
                    {
                        if (_trackedFuelSystems.Count > 0)
                        {
                            ModLogger.Warning($"FuelNetwork: Snapshot vehicle NetworkID:{it.VehicleGuid} not found in tracked systems (have {_trackedFuelSystems.Count} tracked)");
                        }
                    }
                }
                ModLogger.Debug($"FuelNetwork: Applied snapshot with {msg.Items.Length} items");
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: OnFuelSnapshotReceived error", ex);
            }
        }

        private void Broadcast(MiniP2PMessage message)
        {
            var lobby = Singleton<Lobby>.Instance;
            if (lobby == null || !lobby.IsInLobby) return;

            // Serialize once
            var data = MiniMessageSerializer.SerializeMessage(message);
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby.LobbySteamID);
            ModLogger.Debug($"FuelNetwork: Broadcasting {message.MessageType} to {memberCount} members ({data.Length} bytes)");
            
            for (int i = 0; i < memberCount; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby.LobbySteamID, i);
                if (member == lobby.LocalPlayerID) continue;
                ModLogger.Debug($"FuelNetwork: Sending to member {member.m_SteamID}");
                SafeSendPacket(member, data);
            }
        }

        private void BroadcastToOthers(MiniP2PMessage message, CSteamID excludePlayer)
        {
            var lobby = Singleton<Lobby>.Instance;
            if (lobby == null || !lobby.IsInLobby) return;

            // Serialize once
            var data = MiniMessageSerializer.SerializeMessage(message);
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby.LobbySteamID);
            
            for (int i = 0; i < memberCount; i++)
            {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(lobby.LobbySteamID, i);
                if (member == lobby.LocalPlayerID || member == excludePlayer) continue;
                SafeSendPacket(member, data);
            }
        }

        private void SendTo(CSteamID target, MiniP2PMessage message)
        {
            var data = MiniMessageSerializer.SerializeMessage(message);
            ModLogger.Debug($"FuelNetwork: Sending {message.MessageType} to {target.m_SteamID} ({data.Length} bytes)");
            SafeSendPacket(target, data);
        }

        private void SafeSendPacket(CSteamID target, byte[] data)
        {
            try
            {
#if !MONO
                // Pin a copy in IL2CPP for safety
                byte[] copy = new byte[data.Length];
                Array.Copy(data, copy, data.Length);
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(copy, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    bool result = SteamNetworking.SendP2PPacket(target, copy, (uint)copy.Length, EP2PSend.k_EP2PSendReliable, P2P_CHANNEL);
                    ModLogger.Debug($"FuelNetwork: SendP2PPacket result: {result} to {target.m_SteamID} on channel {P2P_CHANNEL}");
                }
                finally
                {
                    handle.Free();
                }
#else
                bool result = SteamNetworking.SendP2PPacket(target, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable, P2P_CHANNEL);
                ModLogger.Debug($"FuelNetwork: SendP2PPacket result: {result} to {target.m_SteamID} on channel {P2P_CHANNEL}");
#endif
            }
            catch (Exception ex)
            {
                ModLogger.Error($"FuelNetwork: Failed sending packet to {target.m_SteamID}", ex);
            }
        }

        private void OnSessionRequest(P2PSessionRequest_t e)
        {
            try
            {
                var who = e.m_steamIDRemote;
                SteamNetworking.AcceptP2PSessionWithUser(who);
            }
            catch (Exception ex)
            {
                ModLogger.Error("FuelNetwork: OnSessionRequest error", ex);
            }
        }

        private void OnSessionConnectFail(P2PSessionConnectFail_t e)
        {
            ModLogger.Warning($"FuelNetwork: P2P connect fail {e.m_eP2PSessionError} with {e.m_steamIDRemote.m_SteamID}");
        }

        private static bool IsHost()
        {
            var lobby = Singleton<Lobby>.Instance;
            return lobby != null && lobby.IsInLobby && lobby.IsHost;
        }

        private static CSteamID GetHostId()
        {
            var lobby = Singleton<Lobby>.Instance;
            if (lobby == null || !lobby.IsInLobby) return CSteamID.Nil;
            return SteamMatchmaking.GetLobbyOwner(lobby.LobbySteamID);
        }
    }
}


