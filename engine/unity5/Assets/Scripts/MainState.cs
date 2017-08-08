﻿using UnityEngine;
using System.Collections;
using BulletUnity;
using BulletSharp;
using System;
using System.Collections.Generic;
using BulletSharp.SoftBody;
using UnityEngine.SceneManagement;
using System.IO;
using Assets.Scripts.FEA;
using Assets.Scripts.FSM;
using System.Linq;
using Assets.Scripts.BUExtensions;

public class MainState : SimState
{
    const float ResetVelocity = 0.05f;
    private const int SolverIterations = 100;

    private BPhysicsWorld physicsWorld;
    private int lastFrameCount;

    private bool tracking;
    private bool awaitingReplay;

    private UnityPacket unityPacket;

    private DynamicCamera dynamicCamera;
    public GameObject dynamicCameraObject;

    private RobotCamera robotCamera;
    public GameObject robotCameraObject;

    //Testing camera location, can be deleted later
    private Vector3 robotCameraPosition = new Vector3(0f, 0.5f, 0f);
    private Vector3 robotCameraRotation = new Vector3(0f, 0f, 0f);
    private Vector3 robotCameraPosition2 = new Vector3(0f, 0f, 0f);
    private Vector3 robotCameraRotation2 = new Vector3(0f, 0f, 0f);
    private Vector3 robotCameraPosition3 = new Vector3(0f, 0.5f, 0f);
    private Vector3 robotCameraRotation3 = new Vector3(0f, 180f, 0f);
    //Testing camera location, can be deleted later

    //=================================IN PROGRESS=============================
    //private UltraSensor ultraSensor;
    //private GameObject ultraSensorObject;
    //=========================================================================

    private GameObject fieldObject;
    private UnityFieldDefinition fieldDefinition;

    private GameObject robotObject;
    private RigidNode_Base rootNode;

    private Vector3 robotStartPosition = new Vector3(0f, 1f, 0f);
    private Vector3 nodeToRobotOffset;
    private BulletSharp.Math.Matrix robotStartOrientation = BulletSharp.Math.Matrix.Identity;
    private const float HOLD_TIME = 0.8f;
    private float keyDownTime = 0f;

    private List<GameObject> extraElements;

    private Texture2D buttonTexture;
    private Texture2D buttonSelected;
    private Texture2D greyWindowTexture;
    private Texture2D darkGreyWindowTexture;
    private Texture2D lightGreyWindowTexture;
    private Texture2D transparentWindowTexture;

    private Font gravityRegular;
    private Font russoOne;

    private GUIController gui;

    private GUIStyle menuWindow;
    private GUIStyle menuButton;

    private OverlayWindow oWindow;

    private System.Random random;

    private FixedQueue<List<ContactDescriptor>> contactPoints;

    //Flags to tell different types of reset
    private bool isResettingOrientation;
    public bool IsResetting { get; set; }

    private DriverPractice driverPractice;

    public List<Tracker> Trackers { get; private set; }

    public static bool ControlsDisabled = false;

    private string fieldPath;
    private string robotPath;

    public override void Awake()
    {
        Environment.SetEnvironmentVariable("MONO_REFLECTION_SERIALIZER", "yes");
        GImpactCollisionAlgorithm.RegisterAlgorithm((CollisionDispatcher)BPhysicsWorld.Get().world.Dispatcher);
        BPhysicsWorld.Get().DebugDrawMode = DebugDrawModes.DrawWireframe | DebugDrawModes.DrawConstraints | DebugDrawModes.DrawConstraintLimits;
        BPhysicsWorld.Get().DoDebugDraw = false;
        BPhysicsWorld.Get().fixedTimeStep = Tracker.FixedTimeStep;
        ((DynamicsWorld)BPhysicsWorld.Get().world).SolverInfo.NumIterations = SolverIterations;
    }

    public override void OnGUI()
    {
        if (gui == null)
        {
            //Custom style for windows
            menuWindow = new GUIStyle(GUI.skin.window);
            menuWindow.normal.background = transparentWindowTexture;
            menuWindow.onNormal.background = transparentWindowTexture;
            menuWindow.font = russoOne;

            //Custom style for buttons
            menuButton = new GUIStyle(GUI.skin.button);
            menuButton.font = russoOne;
            menuButton.normal.background = buttonTexture;
            menuButton.hover.background = buttonSelected;
            menuButton.active.background = buttonSelected;
            menuButton.onNormal.background = buttonSelected;
            menuButton.onHover.background = buttonSelected;
            menuButton.onActive.background = buttonSelected;

            gui = new GUIController();
            gui.hideGuiCallback = HideGUI;
            gui.showGuiCallback = ShowGUI;

            gui.AddWindow("Reset Robot", new DialogWindow("Reset Robot", "Quick Reset", "Reset Spawnpoint"), (object o) =>
            {
                HideGUI();
                switch ((int)o)
                {
                    case 0:
                        BeginReset();
                        EndReset();
                        break;
                    case 1:
                        IsResetting = true;
                        BeginReset();
                        break;

                }

            });

            CreateOrientWindow();

            //Added a robot view to toggle among cameras on robot
            gui.AddWindow("Switch View", new DialogWindow("Switch View", "Driver Station", "Orbit Robot", "Freeroam", "Overview", "Robot view"), (object o) =>
            {
                HideGUI();

                switch ((int)o)
                {
                    case 0:
                        ToDynamicCamera();
                        dynamicCamera.SwitchCameraState(new DynamicCamera.DriverStationState(dynamicCamera));
                        break;
                    case 1:
                        ToDynamicCamera();
                        dynamicCamera.SwitchCameraState(new DynamicCamera.OrbitState(dynamicCamera));
                        DynamicCamera.MovingEnabled = true;
                        break;
                    case 2:
                        ToDynamicCamera();
                        dynamicCamera.SwitchCameraState(new DynamicCamera.FreeroamState(dynamicCamera));
                        break;
                    case 3:
                        ToDynamicCamera();
                        dynamicCamera.SwitchCameraState(new DynamicCamera.OverviewState(dynamicCamera));
                        break;
                    case 4:
                        if (robotCameraObject.GetComponent<RobotCamera>().CurrentCamera != null)
                        {
                            ToRobotCamera();
                        }
                        break;

                }
            });


            gui.AddWindow("Quit to Main Menu", new DialogWindow("Quit to Main Menu?", "Yes", "No"), (object o) =>
            {
                if ((int)o == 0)
                    SceneManager.LoadScene("MainMenu");
            });

            gui.AddWindow("Quit to Desktop", new DialogWindow("Quit to Desktop?", "Yes", "No"), (object o) =>
            {
                if ((int)o == 0)
                    Application.Quit();
            });
        }

        if (Input.GetMouseButtonUp(0) && !gui.ClickedInsideWindow())
        {
            HideGUI();
            gui.HideAllWindows();
        }

        GUI.Window(1, new Rect(0, 0, gui.GetSidebarWidth(), 25), (int windowID) =>
        {
            if (GUI.Button(new Rect(0, 0, gui.GetSidebarWidth(), 25), "Menu", menuButton))
                gui.EscPressed();
        },
            "",
            menuWindow
        );

        gui.Render();
        UserMessageManager.Render();
    }

    void CreateOrientWindow()
    {
        List<string> titles = new List<string>();
        titles.Add("Left");
        titles.Add("Right");
        titles.Add("Forward");
        titles.Add("Back");
        titles.Add("Save Orientation");
        titles.Add("Close");
        titles.Add("Default");

        List<Rect> rects = new List<Rect>();
        rects.Add(new Rect(40, 200, 105, 35));
        rects.Add(new Rect(245, 200, 105, 35));
        rects.Add(new Rect(147, 155, 105, 35));
        rects.Add(new Rect(147, 245, 105, 35));
        rects.Add(new Rect(110, 95, 190, 35));
        rects.Add(new Rect(270, 50, 90, 35));
        rects.Add(new Rect(50, 50, 90, 35));

        oWindow = new TextWindow("Orient Robot", new Rect((Screen.width / 2) - 150, (Screen.height / 2) - 125, 400, 300),
                                             new string[0], new Rect[0], titles.ToArray(), rects.ToArray());
        //The directional buttons lift the robot to avoid collison with objects, rotates it, and saves the applied rotation to a vector3
        gui.AddWindow("Orient Robot", oWindow, (object o) =>
        {
            if (!isResettingOrientation)
            {
                BeginReset();
                TransposeRobot(new Vector3(0f, 1f, 0f));
                isResettingOrientation = true;
            }

            switch ((int)o)
            {
                case 0:
                    RotateRobot(new Vector3(Mathf.PI * 0.25f, 0f, 0f));

                    break;
                case 1:
                    RotateRobot(new Vector3(-Mathf.PI * 0.25f, 0f, 0f));

                    break;
                case 2:
                    RotateRobot(new Vector3(0f, 0f, Mathf.PI * 0.25f));

                    break;
                case 3:
                    RotateRobot(new Vector3(0f, 0f, -Mathf.PI * 0.25f));

                    break;
                case 4:
                    robotStartOrientation = ((RigidNode)rootNode.ListAllNodes()[0]).MainObject.GetComponent<BRigidBody>().GetCollisionObject().WorldTransform.Basis;
                    robotStartOrientation.ToUnity();
                    EndReset();

                    break;
                case 5:
                    BeginReset();
                    oWindow.Active = false;
                    EndReset();

                    break;
                case 6:
                    robotStartOrientation = BulletSharp.Math.Matrix.Identity;
                    robotStartPosition = new Vector3(0f, 1f, 0f);
                    EndReset();
                    break;
            }
        });
    }

    void HideGUI()
    {
        gui.guiVisible = false;
        DynamicCamera.MovingEnabled = true;
    }

    void ShowGUI()
    {
        DynamicCamera.MovingEnabled = false;
    }

    public override void Start()
    {
        physicsWorld = BPhysicsWorld.Get();
        lastFrameCount = physicsWorld.frameCount;

        Trackers = new List<Tracker>();

        unityPacket = new UnityPacket();
        unityPacket.Start();

        extraElements = new List<GameObject>();

        random = new System.Random();

        buttonTexture = Resources.Load("Images/greyButton") as Texture2D;
        buttonSelected = Resources.Load("Images/selectedbuttontexture") as Texture2D;
        gravityRegular = Resources.Load("Fonts/Gravity-Regular") as Font;
        russoOne = Resources.Load("Fonts/Russo_One") as Font;
        greyWindowTexture = Resources.Load("Images/greyBackground") as Texture2D;
        darkGreyWindowTexture = Resources.Load("Images/darkGreyBackground") as Texture2D;
        lightGreyWindowTexture = Resources.Load("Images/lightGreyBackground") as Texture2D;
        transparentWindowTexture = Resources.Load("Images/transparentBackground") as Texture2D;

        contactPoints = new FixedQueue<List<ContactDescriptor>>(Tracker.Length);
        isResettingOrientation = false;

        Controls.LoadControls();

        string selectedReplay = PlayerPrefs.GetString("simSelectedReplay");

        if (string.IsNullOrEmpty(selectedReplay))
        {
            tracking = true;
            Debug.Log(LoadField(PlayerPrefs.GetString("simSelectedField")) ? "Load field success!" : "Load field failed.");
            Debug.Log(LoadRobot(PlayerPrefs.GetString("simSelectedRobot")) ? "Load robot success!" : "Load robot failed.");
        }
        else
        {
            awaitingReplay = true;
            LoadReplay(selectedReplay);
        }

        dynamicCameraObject = GameObject.Find("Main Camera");
        dynamicCamera = dynamicCameraObject.AddComponent<DynamicCamera>();

        DynamicCamera.MovingEnabled = true;
    }

    public override void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            gui.EscPressed();
        //Debug.Log(ultraSensor.ReturnOutput());

        //Reset hot key, start counting time whenever it's pressed down
        if (Input.GetKeyDown(Controls.ControlKey[(int)Controls.Control.ResetRobot]) && !IsResetting)
        {
            keyDownTime = Time.time;
        }

        if (Input.GetKeyUp(Controls.ControlKey[(int)Controls.Control.ResetRobot]) && !IsResetting)
        {
            //Enter reset spawnpoint mode when long hold reset key
            if (Time.time - keyDownTime > HOLD_TIME)
            {
                IsResetting = true;
                BeginReset();
            }
            else
            {
                BeginReset();
                EndReset();
            }
        }

        // Will switch the camera state with the camera toggle button
        if (Input.GetKeyDown(Controls.ControlKey[(int)Controls.Control.CameraToggle]))
        {
            if (dynamicCameraObject.activeSelf && DynamicCamera.MovingEnabled)
            {
                //Switch to robot camera after overview (make sure robot camera exists first)
                if (dynamicCamera.cameraState.GetType().Equals(typeof(DynamicCamera.OverviewState))
                    && robotCameraObject.GetComponent<RobotCamera>().CurrentCamera != null && GameObject.Find("RobotCameraPanel") == null)
                {
                    ToRobotCamera();
                }

                //Toggle afterwards and will not activate dynamic camera
                dynamicCamera.ToggleCameraState(dynamicCamera.cameraState);

            }
            else if (robotCameraObject.activeSelf)
            {
                //Switch to dynamic camera after the last camera
                if (robotCamera.IsLastCamera())
                {
                    //Need to toggle before switching to dynamic because toggling will activate current camera
                    robotCamera.ToggleCamera();
                    ToDynamicCamera();
                }
                else
                {
                    robotCamera.ToggleCamera();
                }
            }
        }

        if (!IsResetting && Input.GetKey(KeyCode.Space))
        {
            contactPoints.Add(null);
            StateMachine.Instance.PushState(new ReplayState(fieldPath, robotPath, contactPoints, Trackers));
        }

        UpdateTrackers();
    }

    public override void FixedUpdate()
    {
        if (rootNode != null)
        {
            UnityPacket.OutputStatePacket packet = unityPacket.GetLastPacket();

            if (!ControlsDisabled) DriveJoints.UpdateAllMotors(rootNode, packet.dio);
        }

        if (IsResetting)
        {
            Resetting();
        }

        //This line is essential for the reset to work accurately
        robotCameraObject.transform.position = robotObject.transform.GetChild(0).transform.position;

        UpdateTrackers();
    }

    public override void LateUpdate()
    {
        if (awaitingReplay)
        {
            awaitingReplay = false;
            StateMachine.Instance.PushState(new ReplayState(fieldPath, robotPath, contactPoints, Trackers));
        }
    }

    public override void Resume()
    {
        lastFrameCount = physicsWorld.frameCount;
        tracking = true;

        Resources.FindObjectsOfTypeAll<Canvas>()[0].enabled = true;

        contactPoints.Clear(null);
    }

    public override void Pause()
    {
        tracking = false;
        Resources.FindObjectsOfTypeAll<Canvas>()[0].enabled = false;

        ToDynamicCamera();
    }

    bool LoadField(string directory)
    {
        fieldPath = directory;

        fieldObject = new GameObject("Field");

        FieldDefinition.Factory = delegate (Guid guid, string name)
        {
            return new UnityFieldDefinition(guid, name);
        };

        string loadResult;
        //Change to .field file. Maybe FieldProperties? Also need to look at field definition
        fieldDefinition = (UnityFieldDefinition)BXDFProperties.ReadProperties(directory + "\\definition.bxdf", out loadResult);
        Debug.Log(loadResult);
        fieldDefinition.CreateTransform(fieldObject.transform);
        return fieldDefinition.CreateMesh(directory + "\\mesh.bxda");
    }

    bool LoadRobot(string directory)
    {
        robotPath = directory;

        robotObject = new GameObject("Robot");
        robotObject.transform.position = robotStartPosition;

        RigidNode_Base.NODE_FACTORY = delegate (Guid guid)
        {
            return new RigidNode(guid);
        };

        List<RigidNode_Base> nodes = new List<RigidNode_Base>();
        //Read .robot instead. Maybe need a RobotSkeleton class
        rootNode = BXDJSkeleton.ReadSkeleton(directory + "\\skeleton.bxdj");
        rootNode.ListAllNodes(nodes);

        foreach (RigidNode_Base n in nodes)
        {
            RigidNode node = (RigidNode)n;

            node.CreateTransform(robotObject.transform);

            if (!node.CreateMesh(directory + "\\" + node.ModelFileName))
            {
                Debug.Log("Robot not loaded!");
                UnityEngine.Object.Destroy(robotObject);
                return false;
            }

            if (n.HasDriverMeta<WheelDriverMeta>() && n.GetDriverMeta<WheelDriverMeta>().type != WheelType.NOT_A_WHEEL)
            {
                WheelType wheelType = n.GetDriverMeta<WheelDriverMeta>().type;
                node.MainObject.AddComponent<BRaycastWheel>().CreateWheel(node);
                node.MainObject.transform.parent = ((RigidNode)node.GetParent()).MainObject.transform;
                continue;
            }

            node.CreateJoint();

            node.MainObject.AddComponent<Tracker>().Trace = true;

            Tracker t = node.MainObject.GetComponent<Tracker>();
            Debug.Log(t);
        }

        driverPractice = robotObject.AddComponent<DriverPractice>();

        //For Ultrasonic testing purposes
        //ultraSensorObject = GameObject.Find("node_0.bxda");
        //ultraSensor = ultraSensorObject.AddComponent<UltraSensor>();

        nodeToRobotOffset = robotObject.transform.GetChild(0).transform.position - robotObject.transform.position;
        //Robot camera feature
        if (robotCamera == null)
        {
            robotCameraObject = GameObject.Find("RobotCameraList");
            robotCamera = robotCameraObject.AddComponent<RobotCamera>();
        }

        robotCamera.RemoveCameras();
        //The camera data should be read here as a foreach loop and included in robot file
        //Attached to main frame and face the front
        robotCamera.AddCamera(robotObject.transform.GetChild(0).transform, robotCameraPosition, robotCameraRotation);
        //Attached to the first node and face the front
        if (robotObject.transform.childCount > 1)
            robotCamera.AddCamera(robotObject.transform.GetChild(1).transform, robotCameraPosition2, robotCameraRotation2);
        //Attached to main frame and face the back
        robotCamera.AddCamera(robotObject.transform.GetChild(0).transform, robotCameraPosition3, robotCameraRotation3);

        robotCameraObject.SetActive(true);


        RotateRobot(robotStartOrientation);

        return true;
    }

    void LoadReplay(string name)
    {
        List<FixedQueue<StateDescriptor>> fieldStates;
        List<FixedQueue<StateDescriptor>> robotStates;
        Dictionary<string, List<FixedQueue<StateDescriptor>>> gamePieceStates;
        List<List<KeyValuePair<ContactDescriptor, int>>> contacts;

        string simSelectedField;
        string simSelectedRobot;

        ReplayImporter.Read(name, out simSelectedField, out simSelectedRobot, out fieldStates, out robotStates, out gamePieceStates, out contacts);

        LoadField(simSelectedField);
        LoadRobot(simSelectedRobot);

        List<Tracker> robotTrackers = Trackers.Where(x => x.transform.parent.name.Equals("Robot")).ToList();
        List<Tracker> fieldTrackers = Trackers.Except(robotTrackers).ToList();

        int i = 0;

        foreach (Tracker t in fieldTrackers)
        {
            t.States = fieldStates[i];
            i++;
        }

        i = 0;

        foreach (Tracker t in robotTrackers)
        {
            t.States = robotStates[i];
            i++;
        }

        foreach (KeyValuePair<string, List<FixedQueue<StateDescriptor>>> k in gamePieceStates)
        {
            GameObject referenceObject = GameObject.Find(k.Key);

            if (referenceObject == null)
                continue;

            foreach (FixedQueue<StateDescriptor> f in k.Value)
            {
                GameObject currentPiece = UnityEngine.Object.Instantiate(referenceObject);
                currentPiece.name = "clone_" + k.Key;
                currentPiece.GetComponent<Tracker>().States = f;
            }
        }

        foreach (var c in contacts)
        {
            if (c != null)
            {
                List<ContactDescriptor> currentContacts = new List<ContactDescriptor>();

                foreach (var d in c)
                {
                    ContactDescriptor currentContact = d.Key;
                    currentContact.RobotBody = robotTrackers[d.Value].GetComponent<BRigidBody>();
                    currentContacts.Add(currentContact);
                }

                contactPoints.Add(currentContacts);
            }
            else
            {
                contactPoints.Add(null);
            }
        }
    }

    public bool ChangeRobot(string directory)
    {
        if (GameObject.Find("Robot") != null) GameObject.Destroy(GameObject.Find("Robot"));
        return LoadRobot(directory);
    }

    private void UpdateTrackers()
    {
        int numSteps = physicsWorld.frameCount - lastFrameCount;

        if (tracking && numSteps > 0)
        {
            foreach (Tracker t in Trackers)
                t.AddState(numSteps);

            for (int i = numSteps; i > 0; i--)
            {
                List<ContactDescriptor> frameContacts = null;
                
                int numManifolds = physicsWorld.world.Dispatcher.NumManifolds;
                
                for (int j = 0; j < numManifolds; j++)
                {
                    PersistentManifold contactManifold = physicsWorld.world.Dispatcher.GetManifoldByIndexInternal(j);
                    BRigidBody obA = contactManifold.Body0.UserObject as BRigidBody;
                    BRigidBody obB = contactManifold.Body1.UserObject as BRigidBody;

                    if ((obA == null || obB == null) || (!obA.gameObject.name.StartsWith("node") && !obB.gameObject.name.StartsWith("node")))
                        continue;

                    ManifoldPoint mp = null;

                    int numContacts = contactManifold.NumContacts;

                    for (int k = 0; k < numContacts; k++)
                    {
                        mp = contactManifold.GetContactPoint(k);

                        if (mp.LifeTime == i)
                            break;
                    }

                    if (mp == null)
                        continue;

                    if (frameContacts == null)
                        frameContacts = new List<ContactDescriptor>();

                    frameContacts.Add(new ContactDescriptor
                    {
                        AppliedImpulse = mp.AppliedImpulse,
                        Position = (mp.PositionWorldOnA + mp.PositionWorldOnB) * 0.5f,
                        RobotBody = obA.name.StartsWith("node") ? obA : obB
                    });
                }

                contactPoints.Add(frameContacts);
            }
        }

        lastFrameCount += numSteps;
    }

    /// <summary>
    /// Return the robot to robotStartPosition and destroy extra game pieces
    /// </summary>
    /// <param name="resetTransform"></param>

    public void BeginReset()
    {
        foreach (Tracker t in UnityEngine.Object.FindObjectsOfType<Tracker>())
            t.Clear();

        foreach (RigidNode n in rootNode.ListAllNodes())
        {
            BRigidBody br = n.MainObject.GetComponent<BRigidBody>();

            if (br == null)
                continue;

            RigidBody r = (RigidBody)br.GetCollisionObject();
            r.LinearVelocity = r.AngularVelocity = BulletSharp.Math.Vector3.Zero;
            r.LinearFactor = r.AngularFactor = BulletSharp.Math.Vector3.Zero;
            
            BulletSharp.Math.Matrix newTransform = r.WorldTransform;
            newTransform.Origin = (robotStartPosition + n.ComOffset).ToBullet();
            newTransform.Basis = BulletSharp.Math.Matrix.Identity;
            r.WorldTransform = newTransform;
        }

        RotateRobot(robotStartOrientation);

        foreach (GameObject g in extraElements)
            UnityEngine.Object.Destroy(g);


        if (IsResetting)
        {
            Debug.Log("is resetting!");
        }
    }

    /// <summary>
    /// Can move robot around in this state, update robotStartPosition if hit enter
    /// </summary>
    void Resetting()
    {
        if (Input.GetMouseButton(1))
        {
            //Transform rotation along the horizontal plane
            Vector3 rotation = new Vector3(0f,
                Input.GetKey(KeyCode.RightArrow) ? ResetVelocity : Input.GetKey(KeyCode.LeftArrow) ? -ResetVelocity : 0f,
                0f);
            if (!rotation.Equals(Vector3.zero))
                RotateRobot(rotation);

        }
        else
        {
            //Transform position
            Vector3 transposition = new Vector3(
                Input.GetKey(KeyCode.RightArrow) ? ResetVelocity : Input.GetKey(KeyCode.LeftArrow) ? -ResetVelocity : 0f,
                0f,
                Input.GetKey(KeyCode.UpArrow) ? ResetVelocity : Input.GetKey(KeyCode.DownArrow) ? -ResetVelocity : 0f);

            if (!transposition.Equals(Vector3.zero))
                TransposeRobot(transposition);
        }

        //Update robotStartPosition when hit enter
        if (Input.GetKey(KeyCode.Return))
        {
            robotStartOrientation = ((RigidNode)rootNode.ListAllNodes()[0]).MainObject.GetComponent<BRigidBody>().GetCollisionObject().WorldTransform.Basis;
            robotStartPosition = robotObject.transform.GetChild(0).transform.position - nodeToRobotOffset;
            //Debug.Log(robotStartPosition);
            EndReset();
        }
    }

    /// <summary>
    /// Put robot back down and switch back to normal state
    /// </summary>
    public void EndReset()
    {
        IsResetting = false;
        isResettingOrientation = false;

        foreach (RigidNode n in rootNode.ListAllNodes())
        {
            BRigidBody br = n.MainObject.GetComponent<BRigidBody>();

            if (br == null)
                continue;

            RigidBody r = (RigidBody)br.GetCollisionObject();
            r.LinearFactor = r.AngularFactor = BulletSharp.Math.Vector3.One;
        }

        foreach (Tracker t in UnityEngine.Object.FindObjectsOfType<Tracker>())
        {
            t.Clear();

            contactPoints.Clear(null);

        }
    }

    public void TransposeRobot(Vector3 transposition)
    {
        foreach (RigidNode n in rootNode.ListAllNodes())
        {
            BRigidBody br = n.MainObject.GetComponent<BRigidBody>();

            if (br == null)
                continue;

            RigidBody r = (RigidBody)br.GetCollisionObject();

            BulletSharp.Math.Matrix newTransform = r.WorldTransform;
            newTransform.Origin += transposition.ToBullet();
            r.WorldTransform = newTransform;
        }
    }

    public void RotateRobot(BulletSharp.Math.Matrix rotationMatrix)
    {
        BulletSharp.Math.Vector3? origin = null;

        foreach (RigidNode n in rootNode.ListAllNodes())
        {
            BRigidBody br = n.MainObject.GetComponent<BRigidBody>();

            if (br == null)
                continue;

            RigidBody r = (RigidBody)br.GetCollisionObject();

            if (origin == null)
                origin = r.CenterOfMassPosition;

            BulletSharp.Math.Matrix rotationTransform = new BulletSharp.Math.Matrix();
            rotationTransform.Basis = rotationMatrix;
            rotationTransform.Origin = BulletSharp.Math.Vector3.Zero;

            BulletSharp.Math.Matrix currentTransform = r.WorldTransform;
            BulletSharp.Math.Vector3 pos = currentTransform.Origin;
            currentTransform.Origin -= origin.Value;
            currentTransform *= rotationTransform;
            currentTransform.Origin += origin.Value;

            r.WorldTransform = currentTransform;
        }
    }

    public void RotateRobot(Vector3 rotation)
    {
        RotateRobot(BulletSharp.Math.Matrix.RotationYawPitchRoll(rotation.y, rotation.z, rotation.x));
    }


    //Helper methods to avoid conflicts between main camera and robot cameras
    void ToDynamicCamera()
    {
        dynamicCameraObject.SetActive(true);
        //robotCameraObject.SetActive(false);
        if (robotCameraObject.GetComponent<RobotCamera>().CurrentCamera != null)
        {
            robotCameraObject.GetComponent<RobotCamera>().CurrentCamera.SetActive(false);
        }
    }

    void ToRobotCamera()
    {
        dynamicCameraObject.SetActive(false);
        //robotCameraObject.SetActive(true);
        if (robotCameraObject.GetComponent<RobotCamera>().CurrentCamera != null)
        {
            robotCameraObject.GetComponent<RobotCamera>().CurrentCamera.SetActive(true);
        }
        else
        {
            UserMessageManager.Dispatch("No camera on robot", 2);
        }
    }

    public DriverPractice GetDriverPractice()
    {
        return driverPractice;
    }

    public void ResetRobotOrientation()
    {
        robotStartOrientation = BulletSharp.Math.Matrix.Identity;
        BeginReset();
        EndReset();
    }
    
    public void SaveRobotOrientation()
    {
        robotStartOrientation = ((RigidNode)rootNode.ListAllNodes()[0]).MainObject.GetComponent<BRigidBody>().GetCollisionObject().WorldTransform.Basis;
        robotStartOrientation.ToUnity();
        EndReset();
    }
}