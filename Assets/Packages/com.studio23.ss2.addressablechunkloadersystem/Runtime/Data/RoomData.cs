using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Studio23.SS2.AddressableChunkLoaderSystem.Data
{
    [CreateAssetMenu(menuName = "Studio-23/RoomLoadingSystem/RoomData", fileName = "RoomData")]
    public class RoomData:ScriptableObject
    {
        public AssetReferenceT<SceneAsset> InteriorScene;
        public AssetReferenceT<SceneAsset> ExteriorScene;
        public Vector3 WorldPosition;
        public float RoomLoadRadius = 4;
        public float MinUnloadTimeout = 10;
        [ShowNativeProperty] public FloorData Floor { get; internal set; }

        [SerializeField] List<RoomData> _alwaysLoadRooms;
        public List<RoomData> AlwaysLoadRooms => _alwaysLoadRooms;

        //#TODO figure out how to include FMOD
        //List<FMODBank> banks

        public event Action<RoomData> OnRoomEntered;
        public event Action<RoomData> OnRoomExited;
        
        public event Action<RoomData> OnRoomExteriorLoaded;
        public event Action<RoomData> OnRoomExteriorUnloaded;
        
        public event Action<RoomData> OnRoomInteriorLoaded;
        public event Action<RoomData> OnRoomInteriorUnloaded;
        public event Action<RoomData> OnRoomDependenciesLoaded;

        public void HandleRoomDependenciesLoaded()
        {
            OnRoomDependenciesLoaded?.Invoke(this);
        }
        public void HandleRoomExteriorLoaded()
        {
            OnRoomExteriorLoaded?.Invoke(this);
        }
        
        public void HandleRoomInteriorLoaded()
        {
            OnRoomInteriorLoaded?.Invoke(this);
        }
        
        public void HandleExteriorUnloaded()
        {
            OnRoomExteriorUnloaded?.Invoke(this);
        }
        
        public void HandleRoomInteriorUnloaded()
        {
            OnRoomInteriorUnloaded?.Invoke(this);
        }
        
        public void Initialize(FloorData floor)
        {
            Floor = floor;
        }

        public void HandleRoomEntered()
        {
            OnRoomEntered?.Invoke(this);
        }
        
        public void HandleRoomExited()
        {
            OnRoomExited?.Invoke(this);
        }

        public bool WantsToAlwaysLoad(RoomData room)
        {
            return _alwaysLoadRooms.Contains(room);
        }
        public bool IsPosInLoadingRange(Vector3 position)
        {
            var dir = (position - WorldPosition);
            dir.y = 0;
            return dir.magnitude <= RoomLoadRadius;
        }
    }
}