﻿using System.Collections.Generic;
using BulletSharp;
using OpenTK;

namespace Simulation_RD
{
    class Physics
    {
        const int ArraySizeX = 5, ArraySizeY = 5, ArraySizeZ = 5;

        const float StartPosX = 0, StartPosY = 0, StartPosZ = 0;

        public DiscreteDynamicsWorld World { get; set; }
        CollisionDispatcher dispatcher;
        DbvtBroadphase broadphase;
        List<CollisionShape> collisionShapes = new List<CollisionShape>();
        CollisionConfiguration collisionConf;
        public BulletFieldDefinition f;

        public Physics()
        {
            collisionConf = new DefaultCollisionConfiguration();
            dispatcher = new CollisionDispatcher(collisionConf);

            broadphase = new DbvtBroadphase();
            World = new DiscreteDynamicsWorld(dispatcher, broadphase, null, collisionConf);
            World.Gravity = new Vector3(0, -10, 0);

            //ground
            CollisionShape groundShape = new BoxShape(50, 50, 50);
            collisionShapes.Add(groundShape);

            CollisionObject groundObj = LocalCreateRigidBody(0, Matrix4.CreateTranslation(0, -50, 0), groundShape);
            //groundObj.UserObject = "Ground";

            #region old stuff
            ////dynamic Rigid bodies
            //const float mass = 1.0f;
            //const float mass_s = 100.0f;

            //CollisionShape collShape = new BoxShape(1);
            //collisionShapes.Add(collShape);
            //Vector3 localInertia = collShape.CalculateLocalInertia(mass);

            //var rbInfo = new RigidBodyConstructionInfo(mass, null, collShape, localInertia);

            //const float start_x = StartPosX - ArraySizeX / 2;
            //const float start_y = StartPosY;
            //const float start_z = StartPosZ - ArraySizeZ / 2;

            //int x, y, z;
            //for (y = 0; y < ArraySizeY; y++)
            //{
            //    for (x = 0; x < ArraySizeX; x++)
            //    {
            //        for (z = 0; z < ArraySizeZ; z++)
            //        {
            //            Matrix4 startTransform = Matrix4.CreateTranslation(
            //                new Vector3(
            //                    2 * x + start_x,
            //                    2 * y + start_y,
            //                    2 * z + start_z
            //                    )
            //                );

            //            rbInfo.MotionState = new DefaultMotionState(startTransform);

            //            RigidBody body = new RigidBody(rbInfo);

            //            body.Translate(new Vector3(0, 18, 0));

            //            World.AddRigidBody(body);
            //        }
            //    }
            //}

            //CollisionShape sphere = new SphereShape(3);
            //collisionShapes.Add(sphere);
            //Vector3 localInertia_s = sphere.CalculateLocalInertia(mass_s);

            //var rbInfo_s = new RigidBodyConstructionInfo(mass_s, null, sphere, localInertia_s);

            //Matrix4 startTransform_s = Matrix4.CreateTranslation(
            //    new Vector3(
            //        StartPosX + 2,
            //        StartPosY,
            //        StartPosZ + 2
            //        )
            //    );

            //rbInfo_s.MotionState = new DefaultMotionState(startTransform_s);

            //RigidBody body_s = new RigidBody(rbInfo_s);

            //body_s.Translate(new Vector3(0, 70, 0));

            //World.AddRigidBody(body_s);
            #endregion

            f = BulletFieldDefinition.FromFile(@"C:\Program Files (x86)\Autodesk\Synthesis\Synthesis\Fields\2013\");
            foreach (RigidBody b in f.Bodies)
            {
                World.AddRigidBody(b);
                collisionShapes.Add(b.CollisionShape);
            }
            World.DebugDrawer = new BulletDebugDrawer();

            //rbInfo.Dispose();

        }

        public virtual void Update(float elapsedTime)
        {
            World.StepSimulation(elapsedTime);
            //World.DebugDrawWorld();
        }

        public void ExitPhysics()
        {

            int i;

            for (i = World.NumConstraints - 1; i >= 0; i--)
            {
                TypedConstraint constraint = World.GetConstraint(i);
                World.RemoveConstraint(constraint);
                constraint.Dispose();
            }

            for (i = World.NumCollisionObjects - 1; i >= 0; i--)
            {
                CollisionObject obj = World.CollisionObjectArray[i];
                RigidBody body = obj as RigidBody;
                if (body != null && body.MotionState != null)
                {
                    body.MotionState.Dispose();
                }
                World.RemoveCollisionObject(obj);
                obj.Dispose();
            }

            foreach (CollisionShape shape in collisionShapes) shape.Dispose();
            collisionShapes.Clear();

            World.Dispose();
            broadphase.Dispose();
            if (dispatcher != null)
            {
                dispatcher.Dispose();
            }
            collisionConf.Dispose();
        }

        public RigidBody LocalCreateRigidBody(float mass, Matrix4 startTransform, CollisionShape shape)
        {
            bool isDynamic = (mass != 0.0f);

            Vector3 localInertia = Vector3.Zero;
            if (isDynamic)
                shape.CalculateLocalInertia(mass, out localInertia);

            DefaultMotionState motionState = new DefaultMotionState(startTransform);

            RigidBodyConstructionInfo rbInfo = new RigidBodyConstructionInfo(mass, motionState, shape);
            RigidBody body = new RigidBody(rbInfo);

            World.AddRigidBody(body);

            return body;
        }
    }
}
