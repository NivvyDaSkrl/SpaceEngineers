﻿using System;
using System.Collections.Generic;
using VRageMath;
using Sandbox.Engine.Utils;

namespace Sandbox.Game.Entities
{
    using MyDynamicAABBTree = VRageMath.MyDynamicAABBTree;
    using Sandbox.Common;

    // For space queries on all entities (including children, invisible objects and objects without physics)
    public static class MyGamePruningStructure
    {
        // A tree for each query type.
        // If you query for a specific type, consider adding a new QueryFlag and AABBTree (so that you don't have to filter the result afterwards).
        static MyDynamicAABBTreeD m_aabbTree;
        static MyDynamicAABBTreeD m_targetsTree;
        static MyDynamicAABBTreeD m_sensableTree; //sensor block
        static MyDynamicAABBTreeD m_voxelMapsTree;

        static List<Type> TargetTypes = new List<Type>() 
        { 
            typeof(MyMeteor), 
            typeof(Sandbox.Game.Entities.Character.MyCharacter), 
            typeof(MyCubeGrid),
            typeof(Sandbox.Game.Weapons.MyMissile), 
            typeof(MyFloatingObject), 
        };

        static List<Type> SensableTypes = new List<Type>() 
        { 
            typeof(MyVoxelMap),
            typeof(Character.MyCharacter),
            typeof(MyCubeGrid),
            typeof(MyFloatingObject),
        };

        static MyGamePruningStructure()
        {
            Init();
        }

        public static MyDynamicAABBTreeD GetPrunningStructure()
        {
            return m_aabbTree;
        }

        public static MyDynamicAABBTreeD GetTargetsPrunningStructure()
        {
            return m_targetsTree;
        }

        public static MyDynamicAABBTreeD GetSensablePrunningStructure()
        {
            return m_sensableTree;
        }

        public static MyDynamicAABBTreeD GetAsteroidsPrunningStructure()
        {
            return m_voxelMapsTree;
        }

        static void Init()
        {
            m_aabbTree = new MyDynamicAABBTreeD(MyConstants.GAME_PRUNING_STRUCTURE_AABB_EXTENSION);
            m_targetsTree = new MyDynamicAABBTreeD(MyConstants.GAME_PRUNING_STRUCTURE_AABB_EXTENSION);
            m_sensableTree = new MyDynamicAABBTreeD(MyConstants.GAME_PRUNING_STRUCTURE_AABB_EXTENSION);
            m_voxelMapsTree = new MyDynamicAABBTreeD(MyConstants.GAME_PRUNING_STRUCTURE_AABB_EXTENSION);
        }

        static BoundingBoxD GetEntityAABB(MyEntity entity)
        {
            BoundingBoxD bbox = entity.PositionComp.WorldAABB;

            //Include entity velocity to be able to hit fast moving objects
            if (entity.Physics != null)
            {
                bbox = bbox.Include(entity.WorldMatrix.Translation + entity.Physics.LinearVelocity * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS * 5);
            }

            return bbox;
        }

        public static void Add(MyEntity entity)
        {
            if (entity.GamePruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED) return;  // already inserted

            BoundingBoxD bbox = GetEntityAABB(entity);
            if (bbox.Size == Vector3D.Zero) return;  // don't add entities with zero bounding boxes

            entity.GamePruningProxyId = m_aabbTree.AddProxy(ref bbox, entity, 0);

            bool isTarget = false;
            if (MyFakes.SHOW_FACTIONS_GUI)
            {
                var moduleOwner = entity as IMyComponentOwner<MyIDModule>;
                MyIDModule module;
                if (moduleOwner != null && moduleOwner.GetComponent(out module))
                {
                    isTarget = true;
                }
            }
            foreach (var targetType in TargetTypes)
            {
                if (targetType == entity.GetType())
                {
                    isTarget = true;
                    break;
                }
            }

            if (isTarget)
            {
                entity.TargetPruningProxyId = m_targetsTree.AddProxy(ref bbox, entity, 0);
            }

            if (SensableTypes.Contains(entity.GetType()))
            {
                entity.SensablePruningProxyId = m_sensableTree.AddProxy(ref bbox, entity, 0);
            }
            
            var voxelMap = entity as MyVoxelMap;
            if (voxelMap != null)
            {
                voxelMap.VoxelMapPruningProxyId = m_voxelMapsTree.AddProxy(ref bbox, entity, 0);
            }
        }

        public static void Remove(MyEntity entity)
        {
            if (entity.GamePruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
            {
                m_aabbTree.RemoveProxy(entity.GamePruningProxyId);
                entity.GamePruningProxyId = MyConstants.PRUNING_PROXY_ID_UNITIALIZED;
            }

            if (entity.TargetPruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
            {
                m_targetsTree.RemoveProxy(entity.TargetPruningProxyId);
                entity.TargetPruningProxyId = MyConstants.PRUNING_PROXY_ID_UNITIALIZED;
            }

            if (entity.SensablePruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
            {
                m_sensableTree.RemoveProxy(entity.SensablePruningProxyId);
                entity.SensablePruningProxyId = MyConstants.PRUNING_PROXY_ID_UNITIALIZED;
            }
            
            var voxelMap = entity as MyVoxelMap;
            if (voxelMap != null && voxelMap.VoxelMapPruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
            {
                m_voxelMapsTree.RemoveProxy(voxelMap.VoxelMapPruningProxyId);
                voxelMap.VoxelMapPruningProxyId = MyConstants.PRUNING_PROXY_ID_UNITIALIZED;
            }
        }

        public static void Clear()
        {
            Init();
            m_aabbTree.Clear();
            m_targetsTree.Clear();
            m_sensableTree.Clear();
            m_voxelMapsTree.Clear();
        }

        public static void Move(MyEntity entity)
        {
            if (entity.GamePruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
            {
                BoundingBoxD bbox = GetEntityAABB(entity);

                if (bbox.Size == Vector3D.Zero)  // remove entities with zero bounding boxes
                {
                    Remove(entity);
                    return;
                }

                m_aabbTree.MoveProxy(entity.GamePruningProxyId, ref bbox, Vector3D.Zero);

                if (entity.TargetPruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
                {
                    m_targetsTree.MoveProxy(entity.TargetPruningProxyId, ref bbox, Vector3D.Zero);
                }

                if (entity.SensablePruningProxyId != MyConstants.PRUNING_PROXY_ID_UNITIALIZED)
                {
                    m_sensableTree.MoveProxy(entity.SensablePruningProxyId, ref bbox, Vector3D.Zero);
                }

                var voxelMap = entity as MyVoxelMap;
                if (voxelMap != null)
                {
                    m_voxelMapsTree.MoveProxy(voxelMap.VoxelMapPruningProxyId, ref bbox, Vector3D.Zero);
                }
            }
        }

        public static void GetAllEntitiesInBox<T>(ref BoundingBoxD box, List<T> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllEntitiesInBox");
            m_aabbTree.OverlapAllBoundingBox<T>(ref box, result, 0, false);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void GetAllSensableEntitiesInBox<T>(ref BoundingBoxD box, List<T> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllSensableEntitiesInBox");
            m_sensableTree.OverlapAllBoundingBox<T>(ref box, result, 0, false);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void GetAllVoxelMapsInBox(ref BoundingBoxD box, List<MyVoxelMap> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllVoxelMapsInBox");
            m_voxelMapsTree.OverlapAllBoundingBox<MyVoxelMap>(ref box, result, 0, false);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void GetAllEntitiesInSphere<T>(ref BoundingSphereD sphere, List<T> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllEntitiesInSphere");
            m_aabbTree.OverlapAllBoundingSphere<T>(ref sphere, result, false);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void GetAllVoxelMapsInSphere(ref BoundingSphereD sphere, List<MyVoxelBase> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllVoxelMapsInSphere");
            m_voxelMapsTree.OverlapAllBoundingSphere<MyVoxelBase>(ref sphere, result, false);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void GetAllTargetsInSphere<T>(ref BoundingSphereD sphere, List<T> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllTargetsInSphere");
            m_targetsTree.OverlapAllBoundingSphere<T>(ref sphere, result, false);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void GetAllEntitiesInRay<T>(ref LineD ray, List<MyLineSegmentOverlapResult<T>> result)
        {
            VRageRender.MyRenderProxy.GetRenderProfiler().StartProfilingBlock("MyGamePruningStructure::GetAllEntitiesInRay");
            m_aabbTree.OverlapAllLineSegment<T>(ref ray, result);
            VRageRender.MyRenderProxy.GetRenderProfiler().EndProfilingBlock();
        }

        public static void DebugDraw()
        {
            //BoundingBox box = new BoundingBox(new Vector3(-10000), new Vector3(10000));
            //var ents = GetAllEntitiesInBox(ref box);
            var result = new List<MyEntity>();
            var resultAABBs = new List<BoundingBoxD>();
            m_aabbTree.GetAll(result, true, resultAABBs);
            for (int i = 0; i < result.Count; i++)
            {
                VRageRender.MyRenderProxy.DebugDrawAABB(resultAABBs[i], Vector3.One, 1, 1, false);
            }
        }
    }
}

