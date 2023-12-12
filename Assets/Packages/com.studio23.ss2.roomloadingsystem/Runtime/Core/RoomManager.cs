using System;
using System.Collections.Generic;
using Bdeshi.Helpers.Utility;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using Studio23.SS2.RoomLoadingSystem.Runtime.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio23.SS2.RoomLoadingSystem.Core
{
    [RequireComponent(typeof(RoomLoader))]
    public class RoomManager:MonoBehaviourSingletonPersistent<RoomManager>
    {
        [SerializeField] List<FloorData> _allFloors;
        private RoomLoader _roomLoader;
        public RoomLoader  RoomLoader=> _roomLoader;
        public event Action<FloorData> OnFloorEntered;
        public event Action<FloorData> OnFloorExited;

        /// <summary>
        /// Fired when room entered and loaded
        /// </summary>
        public event Action<RoomData> OnRoomEntered;

        /// <summary>
        /// Fired when room exited and unloaded
        /// </summary>
        public event Action<RoomData> OnRoomExited;

        /// <summary>
        /// Fired when room itself + all the required rooms for the entered room have been loaded
        /// </summary>
        public event Action<RoomData> OnEnteredRoomDependenciesLoaded;

        public RoomData CurrentEnteredRoom => _currentEnteredRoom;
        [Required] [SerializeField] private RoomData _currentEnteredRoom;

        [ShowNativeProperty]
        public FloorData CurrentFloor => _currentEnteredRoom ? _currentEnteredRoom.Floor : null;

        private HashSet<RoomData> _mustLoadRoomExteriors;
        private HashSet<RoomData> _mustLoadRoomInteriors;
        private HashSet<RoomData> _roomsInLoadingRange;
        /// <summary>
        /// IS NOT A LIST THAT CONTIANS ROOMS THAT ARE BEING UNLOADED. FOR INTERNAL USE ONLY
        /// </summary>
        private List<RoomData> _roomsToUnloadListCache;

        //#TODO separate this
        private Transform player;

        protected override void Initialize()
        {
            _mustLoadRoomExteriors = new HashSet<RoomData>();
            _mustLoadRoomInteriors = new HashSet<RoomData>();
            _roomsInLoadingRange = new HashSet<RoomData>();

            _roomLoader = GetComponent<RoomLoader>();

            foreach (var floor in _allFloors)
            {
                floor.Initialize();

                floor.OnFloorEntered += OnFloorEntered;
                floor.OnFloorExited += OnFloorExited;

                foreach (var roomData in floor.RoomsInFloor)
                {
                    roomData.OnRoomEntered += OnRoomEntered;
                    roomData.OnRoomExited += OnRoomExited;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (var floor in _allFloors)
            {
                floor.OnFloorEntered -= OnFloorEntered;
                floor.OnFloorExited -= OnFloorExited;

                foreach (var roomData in floor.RoomsInFloor)
                {
                    roomData.OnRoomEntered -= OnRoomEntered;
                    roomData.OnRoomExited -= OnRoomExited;
                }
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        protected virtual void FindPlayer()
        {
            player = GameObject.FindWithTag("Player").transform;
        }


        public virtual bool CheckIfRoomExteriorShouldBeLoaded(RoomData room)
        {
            if (_currentEnteredRoom == room)
            {
                // Debug.Log($"{room} is current room");
                return true;
            }

            if (_roomsInLoadingRange.Contains(room))
                return true;

            if (_mustLoadRoomExteriors.Contains(room))
            {
                // Debug.Log($"{room} is global must load");
                return true;
            }

            if (_currentEnteredRoom != null)
            {
                if (_currentEnteredRoom.IsAdjacentTo(room))
                {
                    // Debug.Log($"{room} is adjacent to current room {_currentEnteredRoom}");

                    return true;
                }

                if (CurrentFloor != null && CurrentFloor.WantsToAlwaysLoad(room))
                {
                    // Debug.Log($"{room} is must load for current floor {CurrentFloor}");

                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            updateRoomsInPlayerRange();
            //called explicitly to ensure that timer starts on same frame
            RoomLoader.UpdateRoomUnloadTimer();
        }


        private void updateRoomsInPlayerRange()
        {
            if (_currentEnteredRoom == null)
                return;
            if (CurrentFloor == null)
                return;
            foreach (var roomData in CurrentFloor.RoomsInFloor)
            {
                if (roomData.IsPosInLoadingRange(player.transform.position) ||
                    CurrentEnteredRoom == roomData)
                {
                    HandleRoomEnteredLoadingRange(roomData);
                }
                else
                {
                    HandleRoomExitedLoadingRange(roomData);
                }
            }
        }

        public virtual bool CheckIfRoomInteriorShouldBeLoaded(RoomData room)
        {
            if (_currentEnteredRoom == room)
            {
                // Debug.Log($"{room} interior is current entered room");
                return true;
            }

            if (_mustLoadRoomInteriors.Contains(room))
            {
                // Debug.Log($"{room} interior is global must load room");

                return true;
            }

            if (_currentEnteredRoom != null && CurrentFloor != null)
            {
                if (CurrentFloor.WantsToAlwaysLoad(room))
                {
                    // Debug.Log($"{room} interior is current floor's must load room");
                    return true;
                }
            }

            return false;
        }


        public void SetRoomAsMustLoad(RoomData room)
        {
            if(_mustLoadRoomExteriors.Add(room))
            {
                
            }
        }

        public void UnsetRoomAsMustLoad(RoomData room)
        {
            _mustLoadRoomExteriors.Remove(room);
        }

        public void HandleRoomEnteredLoadingRange(RoomData room)
        {
            if (_roomsInLoadingRange.Add(room))
            {
                AddRoomExteriorToLoad(room);
            }
        }

        public void HandleRoomExitedLoadingRange(RoomData room)
        {
            //actually unloading the room requries waiting for timer
            //handled in different function
            _roomsInLoadingRange.Remove(room);
        }

        internal async UniTask AddRoomExteriorToLoad(RoomData room)
        {
            var handle = _roomLoader.AddExteriorLoadRequest(new RoomLoadRequestData(room));
            await handle.LoadScene();
        }
        
        internal  async UniTask AddRoomInteriorToLoad(RoomData room)
        {
            var handle = _roomLoader.AddInteriorLoadRequest(new RoomLoadRequestData(room));
            await handle.LoadScene();
        }

        public async UniTask EnterRoom(RoomData room)
        {
            if (!_roomLoader.RoomExteriorLoadHandles.ContainsKey(room))
            {
                //the room has been entered but the exterior isn't marked as loaded
                //this is possible if we start in this scene from the editor
                //in which case, exterior is already loaded.
                //we just need to add a dummy handle
                //that won't unload the scene as an addressable.
                _roomLoader.addHandleForAlreadyLoadedExterior(room);
            }

            if (_currentEnteredRoom != room)
            {
                var prevFloor = CurrentFloor;
                var prevRoom = _currentEnteredRoom;

                _currentEnteredRoom = room;
                bool isDifferentFloor = prevFloor != _currentEnteredRoom.Floor;

                if (prevRoom != null)
                {
                    ExitRoom(prevRoom);
                    if (isDifferentFloor && prevFloor != null)
                    {
                        ExitFloor(prevFloor);
                    }
                }


                ForceEnterRoom(room);
                OnRoomEntered?.Invoke(room);
                await AddRoomInteriorToLoad(room);

                loadRoomDependencies(room, isDifferentFloor);
            }
        }

        private async UniTask ExitFloor(FloorData prevFloor)
        {
            Debug.Log("exit floor " + prevFloor, prevFloor);
            OnFloorExited?.Invoke(prevFloor);

            foreach (var roomToUnload in prevFloor.RoomsInFloor)
            {
                _roomsInLoadingRange.Remove(roomToUnload);
                // await RemoveRoomExteriorToLoad(roomToUnload);
            }


            // foreach (var roomToUnload in prevFloor.AlwaysLoadRooms)
            // {
            //     await RemoveRoomExteriorToLoad(roomToUnload);
            // }
        }

        private async UniTask ExitRoom(RoomData prevRoom)
        {
            prevRoom.HandleRoomExited();
            OnRoomExited?.Invoke(prevRoom);
            
            // await RemoveRoomInteriorToLoad(prevRoom);
            // foreach (var adjacentRoom in prevRoom.AlwaysLoadRooms)
            // {
            //     await RemoveRoomExteriorToLoad(adjacentRoom);
            // }
        }

        private async UniTask loadRoomDependencies(RoomData room, bool floorNewlyEntered)
        {
            foreach (var adjacentRoom in room.AlwaysLoadRooms)
            {
                Debug.Log(room + $" always load {adjacentRoom}");
                await AddRoomExteriorToLoad(adjacentRoom);
            }

            if (floorNewlyEntered)
            {
                Debug.Log("laod room dep floorNewlyEntered " + floorNewlyEntered);
                if (room.Floor != null)
                {
                    OnFloorEntered?.Invoke(room.Floor);
                    foreach (var roomToLoad in room.Floor.AlwaysLoadRooms)
                    {
                        await AddRoomExteriorToLoad(roomToLoad);
                    }
                }
            }

            OnEnteredRoomDependenciesLoaded?.Invoke(room);
        }
        

        [Button]
        void printRoomsInLoadingRange()
        {
            foreach (var room in _roomsInLoadingRange)
            {
                Debug.Log($"{room} in loading range");
            }
        }

        private void ForceEnterRoom(RoomData room)
        {
            _currentEnteredRoom = room;
            room.HandleRoomEntered();
        }
    }
}