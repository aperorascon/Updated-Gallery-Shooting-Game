﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
using GameDevWare.Serialization;
 
using NativeWebSocket;
 
using UnityEngine;

/// <summary>
///     Manages the rooms of a server connection.
/// </summary>
[Serializable]
public class ExampleRoomController
{
    public delegate void OnBeginRound();

    public delegate void OnBeginRoundCountDown();

    public delegate void OnGotTargetLineUp(ShootingGalleryNewTargetLineUpMessage targetLineup);

    // Network Events
    //==========================
    /// <summary>
    ///     OnNetworkEntityAdd delegate for OnNetworkEntityAdd event.
    /// </summary>
    /// <param name="entity">Then entity that was just added to the room.</param>
    public delegate void OnNetworkEntityAdd(ExampleNetworkedEntity entity);

    /// <summary>
    ///     OnNetworkEntityRemoved delegate for OnNetworkEntityRemoved event.
    /// </summary>
    /// <param name="entity">Then entity that was just removed to the room.</param>
    public delegate void OnNetworkEntityRemoved(ExampleNetworkedEntity entity, ColyseusNetworkedEntityView view);

    //Custom game delegate functions
    public delegate void OnRoomStateChanged(Colyseus.Schema.MapSchema<string> attributes);

    public delegate void OnRoundEnd(Winner winner);

    public delegate void OnScoreUpdate(ShootingGalleryScoreUpdateMessage update);

    public delegate void OnUserStateChanged(MapSchema<string> changes);

    /// <summary>
    ///     Event for when a NetworkEntity is added to the room.
    /// </summary>
    public static OnNetworkEntityAdd onAddNetworkEntity;

    /// <summary>
    ///     Event for when a NetworkEntity is added to the room.
    /// </summary>
    public static OnNetworkEntityRemoved onRemoveNetworkEntity;

    /// <summary>
    ///     Our user object we get upon joining a room.
    /// </summary>
    [SerializeField]
    private static ExampleNetworkedUser _currentNetworkedUser;

    /// <summary>
    ///     The Client that is created when connecting to the Colyseus server.
    /// </summary>
    private ColyseusClient _client;

    private ColyseusSettings _colyseusSettings;

    /// <summary>
    ///     Collection of entity creation callbacks. Callbacks are added to
    ///     the collection when a <see cref="ExampleNetworkedEntity" /> is created.
    ///     The callbacks are invoked and removed from the collection once the
    ///     entity has been added to the room.
    /// </summary>
    private Dictionary<string, Action<ExampleNetworkedEntity>> _creationCallbacks =
        new Dictionary<string, Action<ExampleNetworkedEntity>>();
    //==========================

    // TODO: Replace GameDevWare stuff
    /// <summary>
    ///     Collection for tracking entities that have been added to the room.
    /// </summary>
    private IndexedDictionary<string, ExampleNetworkedEntity> _entities =
        new IndexedDictionary<string, ExampleNetworkedEntity>();

    /// <summary>
    ///     Collection for tracking entity views that have been added to the room.
    /// </summary>
    private IndexedDictionary<string, ExampleNetworkedEntityView> _entityViews =
        new IndexedDictionary<string, ExampleNetworkedEntityView>();

    private ExampleNetworkedEntityFactory _factory;

    /// <summary>
    ///     Used to help calculate the latency of the connection to the server.
    /// </summary>
    private double _lastPing;

    /// <summary>
    ///     Used to help calculate the latency of the connection to the server.
    /// </summary>
    private double _lastPong;

    /// <summary>
    ///     The ID of the room we were just connected to.
    ///     If there is an abnormal disconnect from the current room
    ///     an automatic attempt will be made to reconnect to that room
    ///     with this room ID.
    /// </summary>
    private string _lastRoomId;

    /// <summary>
    ///     Thread responsible for running <see cref="RunPingThread" />
    ///     on a <see cref="ColyseusRoom{T}" />
    /// </summary>
    private Thread _pingThread;

    /// <summary>
    ///     The current or active Room we get when joining or creating a room.
    /// </summary>
    private ColyseusRoom<ExampleRoomState> _room;

    /// <summary>
    ///     The time as received from the server in milliseconds.
    /// </summary>
    private double _serverTime = -1;

    /// <summary>
    ///     Collection for tracking users that have joined the room.
    /// </summary>
    private IndexedDictionary<string, ExampleNetworkedUser> _users =
        new IndexedDictionary<string, ExampleNetworkedUser>();

    /// <summary>
    ///     Used to help calculate the latency of the connection to the server.
    /// </summary>
    private bool _waitForPong;

    /// <summary>
    ///     The name of the room clients will attempt to create or join on the Colyseus server.
    /// </summary>
    public string roomName = "ShootingGalleryRoom";

    private Dictionary<string, object> roomOptionsDictionary = new Dictionary<string, object>();

    /// <summary>
    ///     All the connected rooms.
    /// </summary>
    public List<IColyseusRoom> rooms = new List<IColyseusRoom>();

    /// <summary>
    ///     Returns the synchronized time from the server in milliseconds.
    /// </summary>
    public double GetServerTime
    {
        get { return _serverTime; }
    }

    /// <summary>
    ///     Returns the synchronized time from the server in seconds.
    /// </summary>
    public double GetServerTimeSeconds
    {
        get { return _serverTime / 1000; }
    }

    /// <summary>
    ///     The latency in milliseconds between client and server.
    /// </summary>
    public double GetRoundtripTime
    {
        get { return _lastPong - _lastPing; }
    }

    public ColyseusRoom<ExampleRoomState> Room
    {
        get { return _room; }
    }

    public string LastRoomID
    {
        get { return _lastRoomId; }
    }

    public IndexedDictionary<string, ExampleNetworkedEntity> Entities
    {
        get { return _entities; }
    }

    public IndexedDictionary<string, ExampleNetworkedEntityView> EntityViews
    {
        get { return _entityViews; }
    }

    public Dictionary<string, Action<ExampleNetworkedEntity>> CreationCallbacks
    {
        get { return _creationCallbacks; }
    }

    public ExampleNetworkedUser CurrentNetworkedUser
    {
        get { return _currentNetworkedUser; }
    }

    /// <summary>
    ///     Checks if a <see cref="ExampleNetworkedEntityView" /> exists for
    ///     the given ID.
    /// </summary>
    /// <param name="entityId">The ID of the <see cref="ExampleNetworkedEntity" /> we're checking for.</param>
    /// <returns></returns>
    public bool HasEntityView(string entityId)
    {
        return EntityViews.ContainsKey(entityId);
    }

    /// <summary>
    ///     Returns a <see cref="ExampleNetworkedEntityView" /> given <see cref="entityId" />
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns>
    ///     Returns <see cref="ExampleNetworkedEntityView" /> if one exists for the given <see cref="entityId" />
    /// </returns>
    public ExampleNetworkedEntityView GetEntityView(string entityId)
    {
        if (EntityViews.ContainsKey(entityId))
        {
            return EntityViews[entityId];
        }

        return null;
    }

    public static event OnRoomStateChanged onRoomStateChanged;
    public static event OnGotTargetLineUp onGotTargetLineUp;
    public static event OnScoreUpdate onScoreUpdate;
    public static event OnBeginRoundCountDown onBeginRoundCountDown;
    public static event OnBeginRound onBeginRound;
    public static event OnRoundEnd onRoundEnd;
    public static event OnUserStateChanged OnCurrentUserStateChanged;

    /// <summary>
    ///     Set the dependencies.
    /// </summary>
    /// <param name="roomName"></param>
    /// <param name="settings"></param>
    public void SetDependencies(ColyseusSettings settings)
    {
        _colyseusSettings = settings;
        //add
       // ColyseusClient.onAddRoom += AddRoom;
    }

    public void SetRoomOptions(Dictionary<string, object> options)
    {
        roomOptionsDictionary = options;
    }

    /// <summary>
    ///     Set the <see cref="NetworkedEntitExampleNetworkedEntityFactoryyFactory" /> of the RoomManager.
    /// </summary>
    /// <param name="factory"></param>
    public void SetNetworkedEntityFactory(ExampleNetworkedEntityFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    ///     Set the client of the <see cref="ColyseusRoomManager" />.
    /// </summary>
    /// <param name="client"></param>
    public void SetClient(ColyseusClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Adds the given room to <see cref="rooms" /> and
    ///     initiates its connection to the server.
    /// </summary>
    /// <param name="roomToAdd"></param>
    /// <returns></returns>
    public void AddRoom(IColyseusRoom roomToAdd)
    {
        roomToAdd.OnLeave += code => { rooms.Remove(roomToAdd); };
        rooms.Add(roomToAdd);
    }

    /// <summary>
    ///     Create a room with the given roomId.
    /// </summary>
    /// <param name="roomId">The ID for the room.</param>
    public async Task CreateSpecificRoom(ColyseusClient client, string roomName, string roomId)
    {
        Debug.Log($"Creating Room {roomId}");

        try
        {
            //Populate an options dictionary with custom options provided elsewhere as well as the critical option we need here, roomId
            Dictionary<string, object> options = new Dictionary<string, object> {["roomId"] = roomId};
            foreach (KeyValuePair<string, object> option in roomOptionsDictionary)
            {
                options.Add(option.Key, option.Value);
            }

            _room = await client.Create<ExampleRoomState>(roomName, options);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to create room {roomId} : {ex.Message}");
            return;
        }

        Debug.Log($"Created Room: {_room.Id}");
        _lastRoomId = roomId;
        RegisterRoomHandlers();
    }

    /// <summary>
    ///     Join an existing room or create a new one using <see cref="roomName" /> with no options.
    ///     <para>Locked or private rooms are ignored.</para>
    /// </summary> 
    Action action;
    public async void JoinOrCreateRoom()
    {
        try
        {
            Debug.Log($"Join Or Create Room - Name = {roomName}.... ");
            // Populate an options dictionary with custom options provided elsewhere
            Dictionary<string, object> options = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> option in roomOptionsDictionary)
            {
                options.Add(option.Key, option.Value);
            }

            _room = await _client.JoinOrCreate<ExampleRoomState>(roomName, options);
            var callbacks = Colyseus.Schema.Callbacks.Get(_room);

            callbacks.Listen(state => state, (currentValue, previousValue) => {
                // ...
            }); // when an entity was added (ArraySchema or MapSchema)
              action = callbacks.OnAdd(state => state.attributes, (sessionId, entity) => {
            // .  
        });
             
            Debug.Log($"Joined / Created Room: {_room.Id}");
            _lastRoomId = _room.Id;
            RegisterRoomHandlers();
        }
        catch (Exception e)
        {
            Debug.LogError($"Room Controller Error - {e.Message + e.StackTrace}");
        }
    }
    public void sdsd() {
       
     
 
    }
    public async Task LeaveAllRooms(bool consented, Action onLeave = null)
    {
        if (_room != null && rooms.Contains(_room) == false)
        {
            await _room.Leave(consented);
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            await rooms[i].Leave(consented);
        }

        _entities.Clear();
        _entityViews.Clear();
        _users.Clear();

        ClearRoomHandlers();

        onLeave?.Invoke();
    }

    /// <summary>
    ///     Subscribes the manager to <see cref="room" />'s networked events
    ///     and starts measuring latency to the server.
    /// </summary>
    public virtual void RegisterRoomHandlers()
    {
        Debug.Log($"sessionId: {_room.SessionId}");

        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        _pingThread = new Thread(RunPingThread);
        _pingThread.Start(_room);

        _room.OnLeave += OnLeaveRoom;

        _room.OnStateChange += OnStateChangeHandler;

        _room.OnMessage<ExampleNetworkedUser>("onJoin", currentNetworkedUser =>
        {
            Debug.Log($"Received 'ExampleNetworkedUser' after join/creation call {currentNetworkedUser.id}!");
            Debug.Log(Json.SerializeToString(currentNetworkedUser));

            _currentNetworkedUser = currentNetworkedUser;
        });

        _room.OnMessage<ExampleRFCMessage>("onRFC", _rfc =>
        {
            //Debug.Log($"Received 'onRFC' {_rfc.entityId}!");
            if (_entityViews.Keys.Contains(_rfc.entityId))
            {
                _entityViews[_rfc.entityId].RemoteFunctionCallHandler(_rfc);
            }
        });

        _room.OnMessage<ExamplePongMessage>(0, message =>
        {
            _lastPong = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _serverTime = message.serverTime;
            _waitForPong = false;
        });

        //Custom game logic
        _room.OnMessage<ShootingGalleryNewTargetLineUpMessage>("newTargetLineUp",
            targets => { onGotTargetLineUp?.Invoke(targets); });

        _room.OnMessage<ShootingGalleryScoreUpdateMessage>("onScoreUpdate",
            scoreUpdate => { onScoreUpdate?.Invoke(scoreUpdate); });

        _room.OnMessage<ShootingGalleryMessage>("beginRoundCountDown", msg => { onBeginRoundCountDown?.Invoke(); });

        _room.OnMessage<ShootingGalleryMessage>("beginRound", msg => { onBeginRound?.Invoke(); });

        _room.OnMessage<ShootingGalleryRoundEndMessage>("onRoundEnd", winner => { onRoundEnd?.Invoke(winner.winner); });

        //========================
        Debug.Log($"Adding OnAdd/OnRemove callbacks for all {_room.State.networkedEntities.Count} entities! ***");

        //add
      //  _room.State.networkedEntities.OnAdd += OnEntityAdd;
     //   _room.State.networkedEntities.OnRemove += OnEntityRemoved;

      //  _room.State.networkedUsers.OnAdd += OnUserAdd;
      //  _room.State.networkedEntities.re += OnUserRemove;
      //  _room.State.pla.onAdd( (value, key)=> {/* do stuff */});

        //  _room.State.TriggerAll();no neccessary
        //========================

        _room.Connection.OnError += Room_OnError;
        _room.Connection.OnClose += Room_OnClose;
    }

    private void OnLeaveRoom(int code)
    {
        WebSocketCloseCode closeCode = WebSocketHelpers.ParseCloseCodeEnum(code);
        Debug.Log(string.Format("ROOM: ON LEAVE =- Reason: {0} ({1})",closeCode, code));
        _pingThread.Abort();
        _pingThread = null;
        _room = null;

        if (closeCode != WebSocketCloseCode.Normal && !string.IsNullOrEmpty(_lastRoomId))
        {
            JoinRoomId(_lastRoomId);
        }
    }

    /// <summary>
    ///     Unsubscribes <see cref="Room" /> from networked events."/>
    /// </summary>
    private void ClearRoomHandlers()
    {
        if (_pingThread != null)
        {
            _pingThread.Abort();
            _pingThread = null;
        }

        if (_room == null)
        {
            return;
        }
        //add
      //  _room.State.networkedEntities.OnAdd -= OnEntityAdd;
      //  _room.State.networkedEntities.OnRemove -= OnEntityRemoved;
      //  _room.State.networkedUsers.OnAdd -= OnUserAdd;
       // _room.State.networkedUsers.OnRemove -= OnUserRemove;

        _room.Connection.OnError -= Room_OnError;
        _room.Connection.OnClose -= Room_OnClose;

        _room.OnStateChange -= OnStateChangeHandler;

        _room.OnLeave -= OnLeaveRoom;

        _room = null;
        _currentNetworkedUser = null;
    }

    /// <summary>
    ///     Asynchronously gets all the available rooms of the <see cref="_client" />
    ///     named <see cref="roomName" />
    /// </summary>
  /*  public async Task<ColyseusRoomAvailable[]> GetRoomListAsync()
    {
        ColyseusRoomAvailable[] allRooms = await _client.GetAvailableRooms(roomName);

        return allRooms;
    }*/

    /// <summary>
    ///     Join a room with the given <see cref="roomId" />.
    /// </summary>
    /// <param name="roomId">ID of the room to join.</param>
    public async Task JoinRoomId(string roomId)
    {
        Debug.Log($"Joining Room ID {roomId}....");
        ClearRoomHandlers();

        try
        {
            while (_room == null || !_room.Connection.IsOpen)
            {
                _room = await _client.JoinById<ExampleRoomState>(roomId);

                if (_room == null || !_room.Connection.IsOpen)
                {
                    Debug.Log($"Failed to Connect to {roomId}.. Retrying in 5 Seconds...");
                    await Task.Delay(5000);
                }
            }
            Debug.Log($"Connected to {roomId}..");
            _lastRoomId = roomId;
            RegisterRoomHandlers();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            Debug.LogError("Failed to join room");
            //await CreateSpecificRoom(_client, roomName, roomId, onJoin);
        }
    }

    /// <summary>
    ///     The callback for the event when a <see cref="ExampleNetworkedEntity" /> is added to a room.
    /// </summary>
    /// <param name="entity">The entity that was just added.</param>
    /// <param name="key">The entity's key</param>
    private async void OnEntityAdd(string key, ExampleNetworkedEntity entity)
    {
        Debug.Log(
            $"Entity [{entity.__refId} | {entity.id}] add: x => {entity.xPos}, y => {entity.yPos}, z => {entity.zPos}");

        _entities.Add(entity.id, entity);

        //Creation ID is only Registered with the owner so only owners callback will be triggered
        if (!string.IsNullOrEmpty(entity.creationId) && _creationCallbacks.ContainsKey(entity.creationId))
        {
            _creationCallbacks[entity.creationId].Invoke(entity);
            _creationCallbacks.Remove(entity.creationId);
        }

        onAddNetworkEntity?.Invoke(entity);

        if (_entityViews.ContainsKey(entity.id) == false && !string.IsNullOrEmpty(entity.attributes["prefab"]))
        {
            await _factory.CreateFromPrefab(entity);
        }
    }

    /// <summary>
    ///     The callback for the event when a <see cref="ExampleNetworkedEntity" /> is removed from a room.
    /// </summary>
    /// <param name="entity">The entity that was just removed.</param>
    /// <param name="key">The entity's key</param>
    private void OnEntityRemoved(string key, ExampleNetworkedEntity entity)
    {
        if (_entities.ContainsKey(entity.id))
        {
            _entities.Remove(entity.id);
        }

        ColyseusNetworkedEntityView view = null;

        if (_entityViews.ContainsKey(entity.id))
        {
            view = _entityViews[entity.id];
            _entityViews.Remove(entity.id);
        }

        onRemoveNetworkEntity?.Invoke(entity, view);
    }

    /// <summary>
    ///     Callback for when a <see cref="ExampleNetworkedUser" /> is added to a room.
    /// </summary>
    /// <param name="user">The user object</param>
    /// <param name="key">The user key</param>
    private void OnUserAdd(string key, ExampleNetworkedUser user)
    {
        Debug.Log($"user [{user.__refId} | {user.id} | key {key}] Joined");

        // Add "player" to map of players
        _users.Add(key, user);
        //add
        // On entity update...
        /*     user.OnChange += changes =>
             {
                 user.updateHash = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                 // If the change is for our current user then fire the event with the attributes that changed
                 if (ExampleManager.Instance.CurrentUser != null &&
                     string.Equals(ExampleManager.Instance.CurrentUser.sessionId, user.sessionId))
                 {
                     OnCurrentUserStateChanged?.Invoke(user.attributes);
                 }
             };*/
    }

    /// <summary>
    ///     Callback for when a user is removed from a room.
    /// </summary>
    /// <param name="user">The removed user.</param>
    /// <param name="key">The user key.</param>
    private void OnUserRemove(string key, ExampleNetworkedUser user)
    {
        Debug.Log($"user [{user.__refId} | {user.id} | key {key}] Left");

        _users.Remove(key);
    }

    /// <summary>
    ///     Callback for when the room's connection closes.
    /// </summary>
    /// <param name="closeCode">Code reason for the connection close.</param>
    private static void Room_OnClose(int closeCode)
    {
        Debug.LogError("Room_OnClose: " + closeCode);
    }

    /// <summary>
    ///     Callback for when the room get an error.
    /// </summary>
    /// <param name="errorMsg">The error message.</param>
    private static void Room_OnError(string errorMsg)
    {
        Debug.LogError("Room_OnError: " + errorMsg);
    }

    /// <summary>
    ///     Callback when the room state has changed.
    /// </summary>
    /// <param name="state">The room state.</param>
    /// <param name="isFirstState">Is it the first state?</param>
    private static void OnStateChangeHandler(ExampleRoomState state, bool isFirstState)
    {
        // Setup room first state
        //Debug.Log("State has been updated!");
        onRoomStateChanged?.Invoke(state.attributes);
    }

    /// <summary>
    ///     Sends "ping" message to current room to help measure latency to the server.
    /// </summary>
    /// <param name="roomToPing">The <see cref="ColyseusRoom{T}" /> to ping.</param>
    private void RunPingThread(object roomToPing)
    {
        ColyseusRoom<ExampleRoomState> currentRoom = (ColyseusRoom<ExampleRoomState>) roomToPing;

        const float pingInterval = 0.5f; // seconds
        const float pingTimeout = 15f; //seconds

        int timeoutMilliseconds = Mathf.FloorToInt(pingTimeout * 1000);
        int intervalMilliseconds = Mathf.FloorToInt(pingInterval * 1000);

        DateTime pingStart;
        while (currentRoom != null)
        {
            _waitForPong = true;
            pingStart = DateTime.Now;
            _lastPing = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _ = currentRoom.Send("ping");

            while (currentRoom != null && _waitForPong &&
                   DateTime.Now.Subtract(pingStart).TotalSeconds < timeoutMilliseconds)
            {
                Thread.Sleep(200);
            }

            if (_waitForPong)
            {
                Debug.LogError("Ping Timed out");
            }

            Thread.Sleep(intervalMilliseconds);
        }
    }

    /// <summary>
    ///     Increments the known <see cref="_serverTime" /> by <see cref="Time.fixedDeltaTime" />
    ///     converted into milliseconds.
    /// </summary>
    public void IncrementServerTime()
    {
        _serverTime += Time.fixedDeltaTime * 1000;
    }

    public async void CleanUp()
    {
        _pingThread?.Abort();

        List<Task> leaveRoomTasks = new List<Task>();

        foreach (IColyseusRoom roomEl in rooms)
        {
            leaveRoomTasks.Add(roomEl.Leave(false));
        }

        if (_room != null)
        {
            leaveRoomTasks.Add(_room.Leave(false));
        }

        await Task.WhenAll(leaveRoomTasks.ToArray());
    }
}