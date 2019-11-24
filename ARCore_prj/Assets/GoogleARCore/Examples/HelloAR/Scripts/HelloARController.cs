//-----------------------------------------------------------------------
// <copyright file="HelloARController.cs" company="Google">
//
// Copyright 2017 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// </copyright>
//-----------------------------------------------------------------------

namespace GoogleARCore.Examples.HelloAR
{
    using System.Collections.Generic;
    using GoogleARCore;
    using GoogleARCore.Examples.Common;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

#if UNITY_EDITOR
    // Set up touch input propagation while using Instant Preview in the editor.
    using Input = InstantPreviewInput;
#endif

    /// <summary>
    /// Controls the HelloAR example.
    /// </summary>
    public class HelloARController : MonoBehaviour
    {
        /// <summary>
        /// The first-person camera being used to render the passthrough camera image (i.e. AR
        /// background).
        /// </summary>
        public Camera FirstPersonCamera;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a vertical plane.
        /// </summary>
        public GameObject GameObjectVerticalPlanePrefab;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a horizontal plane.
        /// </summary>
        public GameObject GameObjectHorizontalPlanePrefab;

        /// <summary>
        /// A prefab to place when a raycast from a user touch hits a feature point.
        /// </summary>
        public GameObject GameObjectPointPrefab;

        /// <summary>
        /// The rotation in degrees need to apply to prefab when it is placed.
        /// </summary>
        private const float k_PrefabRotation = 180.0f;

        /// <summary>
        /// True if the app is in the process of quitting due to an ARCore connection error,
        /// otherwise false.
        /// </summary>
        private bool m_IsQuitting = false;

        /// <summary>
        /// The Unity Awake() method.
        /// </summary>
        public void Awake()
        {
            // Enable ARCore to target 60fps camera capture frame rate on supported devices.
            // Note, Application.targetFrameRate is ignored when QualitySettings.vSyncCount != 0.
            Application.targetFrameRate = 60;
            anchors = new List<Anchor>();
            anchors_second = new List<Anchor>();
        }

        private List<Anchor> anchors;
        private List<Anchor> anchors_second;
        [SerializeField] private Material mat_line;
        [SerializeField] private Text debug_part;

        private static float distance_anchors(Anchor one, Pose pose_anchor)
        {
            return (one.transform.position - pose_anchor.position).magnitude;
        }
        /*
        private int? detect_equivalent_anchor(Pose pose_anchor)
        {
            //look all minus last
            for (int i = 0; anchors != null && i < anchors.Count - 1; i++)
            {
                float d = distance_anchors(anchors[i], pose_anchor);
                if (d < 0.3)
                {
                    return i;
                }
            }
            return null;
        }
        
        private void CreateUnionsExistent(int i)
        {
            Anchor anchor = anchors[i];
            CreateLink(anchors[anchors.Count - 1], anchor);
            anchors.Add(anchor);
        }*/

        private void CreateUnions(List<Anchor> anchors, Anchor new_anchor)
        {
            if (anchors.Count == 0)
            {
                anchors.Add(new_anchor);
                return;
            }


            CreateLink(anchors[anchors.Count - 1], new_anchor);

            anchors.Add(new_anchor);
        }

        private void CreateLink(Anchor one, Anchor two)
        {
            GameObject s = new GameObject();
            LineRenderer lr = s.AddComponent<LineRenderer>();
            lr.SetPositions(
                new Vector3[] { one.transform.position,
                                two.transform.position });
            lr.widthMultiplier = 0.13f;
            lr.material = mat_line;

            GameObject ts = new GameObject();
            TMPro.TextMeshPro tsp = ts.AddComponent<TMPro.TextMeshPro>();
            tsp.fontSize = 10;
            tsp.transform.position = ((one.transform.position - two.transform.position) / 2) + two.transform.position;
            tsp.transform.rotation = Quaternion.Euler(-90, 0, 0);
            float distance = (one.transform.position - two.transform.position).magnitude;
            tsp.text = "" + distance;
        }

        private static float area_points(List<Anchor> points)
        {
            float x = (points[0].transform.position - points[1].transform.position).magnitude;
            float y = (points[2].transform.position - points[3].transform.position).magnitude;
            float z = (points[1].transform.position - points[2].transform.position).magnitude;
            float w = (points[0].transform.position - points[3].transform.position).magnitude;

            float z2 = (z + w) / 2;
            float x2 = (x + y) / 2;
            return z2 * x2;
        }

        private float medium_distance()
        {
            float med = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                for(int j = 0; j < anchors_second.Count; j++)
                {
                    med += (anchors[i].transform.position - anchors_second[j].transform.position).magnitude;
                }
            }

            return med / (anchors.Count * anchors_second.Count);
        }

        private float UniteFinal()
        {
            HashSet<int> others_picked = new HashSet<int>();
            for (int i = 0; i < anchors.Count; i++)
            {
                int? min_index = null;
                float? min_distance = null;
                for (int j = 0; j < anchors_second.Count; j++)
                {
                    if (others_picked.Contains(j))
                    {
                        continue;
                    }

                    float Sqrdistance = (anchors[i].transform.position - anchors_second[j].transform.position).sqrMagnitude;
                    if (min_distance == null || Sqrdistance < min_distance.Value)
                    {
                        min_index = j;
                        min_distance = Sqrdistance;
                    }
                }

                others_picked.Add(min_index.Value);
                CreateLink(anchors[i], anchors_second[min_index.Value]);
            }

            float x = area_points(anchors);
            float y = area_points(anchors_second);


            float area = ((x + y) / 2) * medium_distance();
            return area;
        }

        /// <summary>
        /// The Unity Update() method.
        /// </summary>
        public void Update()
        {
            _UpdateApplicationLifecycle();

            // If the player has not touched the screen, we are done with this update.
            Touch touch;
            if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
            {
                return;
            }

            // Should not handle input if the player is pointing on UI.
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                return;
            }

            // Raycast against the location the player touched to search for planes.
            TrackableHit hit;
            TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon |
                TrackableHitFlags.FeaturePointWithSurfaceNormal;

            if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit))
            {
                // Use hit pose and camera pose to check if hittest is from the
                // back of the plane, if it is, no need to create the anchor.
                if ((hit.Trackable is DetectedPlane) &&
                    Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
                        hit.Pose.rotation * Vector3.up) < 0)
                {
                    Debug.Log("Hit at back of the current DetectedPlane");
                }
                else
                {
                    // Choose the prefab based on the Trackable that got hit.
                    GameObject prefab;
                    if (hit.Trackable is FeaturePoint)
                    {
                        prefab = GameObjectPointPrefab;
                    }
                    else if (hit.Trackable is DetectedPlane)
                    {
                        DetectedPlane detectedPlane = hit.Trackable as DetectedPlane;
                        if (detectedPlane.PlaneType == DetectedPlaneType.Vertical)
                        {
                            prefab = GameObjectVerticalPlanePrefab;
                        }
                        else
                        {
                            prefab = GameObjectHorizontalPlanePrefab;
                        }
                    }
                    else
                    {
                        prefab = GameObjectHorizontalPlanePrefab;
                    }
                    var gameObject = Instantiate(prefab, hit.Pose.position, hit.Pose.rotation);

                    // Compensate for the hitPose rotation facing away from the raycast (i.e.
                    // camera).
                    gameObject.transform.Rotate(0, k_PrefabRotation, 0, Space.Self);

                    // Create an anchor to allow ARCore to track the hitpoint as understanding of
                    // the physical world evolves.
                    var anchor = hit.Trackable.CreateAnchor(hit.Pose);
                    if (anchors.Count < 4)
                    {
                        // Instantiate prefab at the hit pose.
                        CreateUnions(anchors, anchor);
                        // Make game object a child of the anchor.
                        gameObject.transform.parent = anchor.transform;
                        if (anchors.Count == 4)
                        {
                            CreateLink(anchors[anchors.Count - 1], anchors[0]);
                        }
                    } else
                    {
                        // Instantiate prefab at the hit pose.
                        CreateUnions(anchors_second, anchor);
                        // Make game object a child of the anchor.
                        gameObject.transform.parent = anchor.transform;
                        if (anchors_second.Count == 4)
                        {
                            CreateLink(anchors_second[anchors_second.Count - 1], anchors_second[0]);
                            float area = UniteFinal();
                            debug_part.text = "Final area is: " + area;
                        }
                    }


                }
            }
        }

        /// <summary>
        /// Check and update the application lifecycle.
        /// </summary>
        private void _UpdateApplicationLifecycle()
        {
            // Exit the app when the 'back' button is pressed.
            if (Input.GetKey(KeyCode.Escape))
            {
                Application.Quit();
            }

            // Only allow the screen to sleep when not tracking.
            if (Session.Status != SessionStatus.Tracking)
            {
                Screen.sleepTimeout = SleepTimeout.SystemSetting;
            }
            else
            {
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
            }

            if (m_IsQuitting)
            {
                return;
            }

            // Quit if ARCore was unable to connect and give Unity some time for the toast to
            // appear.
            if (Session.Status == SessionStatus.ErrorPermissionNotGranted)
            {
                _ShowAndroidToastMessage("Camera permission is needed to run this application.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
            else if (Session.Status.IsError())
            {
                _ShowAndroidToastMessage(
                    "ARCore encountered a problem connecting.  Please start the app again.");
                m_IsQuitting = true;
                Invoke("_DoQuit", 0.5f);
            }
        }

        /// <summary>
        /// Actually quit the application.
        /// </summary>
        private void _DoQuit()
        {
            Application.Quit();
        }

        /// <summary>
        /// Show an Android toast message.
        /// </summary>
        /// <param name="message">Message string to show in the toast.</param>
        private void _ShowAndroidToastMessage(string message)
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject unityActivity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (unityActivity != null)
            {
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject =
                        toastClass.CallStatic<AndroidJavaObject>(
                            "makeText", unityActivity, message, 0);
                    toastObject.Call("show");
                }));
            }
        }
    }
}
