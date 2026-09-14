using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightweightDI
{
    /// <summary>
    /// Builds the controller tree of a scene.
    ///
    /// Unity's transform hierarchy and the controller hierarchy are not the same thing -
    /// there can be any number of plain transforms between two controllers. These helpers
    /// walk the transforms and produce the controller-level parent/child links the bootstrap
    /// and <see cref="ControllerContainer"/> rely on.
    /// </summary>
    public static class ControllerUtils
    {
        /// <summary>
        /// Builds the whole tree for a scene and returns its root controllers - the ones the
        /// bootstrap injects into and initialises.
        /// </summary>
        public static List<Controller> InitializeSceneHierarchy(Scene scene)
        {
            List<Controller> rootControllers = FindRootControllers(scene);
            InitializeControllerHierarchy(rootControllers);
            return rootControllers;
        }

        /// <summary>
        /// Same for a single GameObject - used for prefabs instantiated at runtime, which the
        /// scene bootstrap never sees.
        /// </summary>
        public static void InitializeControllerHierarchy(GameObject gameObject)
        {
            List<Controller> startControllers = new List<Controller>();
            startControllers.AddRange(gameObject.GetComponents<Controller>());
            InitializeControllerHierarchy(startControllers, initializeParents: true);
        }

        public static List<Controller> FindRootControllers(Scene scene)
        {
            List<Controller> result = new List<Controller>();
            GameObject[] rootGameObjects = scene.GetRootGameObjects();

            foreach (GameObject go in rootGameObjects)
            {
                CollectControllers(result, go.transform, includeSelf: true);
            }

            return result;
        }

        /// <summary>
        /// Walks down from the given controllers and links each one to its nearest controller
        /// ancestor, filling the Children list of every container on the way.
        /// </summary>
        /// <param name="initializeParents">
        /// True when the start controllers are not scene roots and their own parents have to
        /// be resolved by walking up first.
        /// </param>
        public static void InitializeControllerHierarchy(List<Controller> startControllers,
            bool initializeParents = false)
        {
            if (initializeParents)
            {
                foreach (Controller controller in startControllers)
                {
                    LinkToNearestParentController(controller);
                }
            }

            Queue<Controller> pending = new Queue<Controller>();
            foreach (Controller controller in startControllers)
            {
                pending.Enqueue(controller);
            }

            while (pending.Count > 0)
            {
                Controller current = pending.Dequeue();

                List<Controller> childControllers = GetControllersInChildren(current.transform);

                foreach (Controller child in childControllers)
                {
                    child.SetControllerParent(current);
                    pending.Enqueue(child);
                }

                if (current is ControllerContainer container)
                {
                    container.Children = childControllers;
                }
            }
        }

        private static void LinkToNearestParentController(Controller controller)
        {
            Transform parent = controller.transform.parent;

            while (parent != null)
            {
                // A container wins over a plain controller on the same object, because the
                // container is what owns children.
                Controller parentController = parent.GetComponent<ControllerContainer>();
                if (parentController == null)
                {
                    parentController = parent.GetComponent<Controller>();
                }

                if (parentController != null)
                {
                    controller.Parent = parentController;

                    foreach (ControllerContainer container in parent.GetComponents<ControllerContainer>())
                    {
                        if (!container.Children.Contains(controller))
                        {
                            container.Children.Add(controller);
                        }
                    }

                    return;
                }

                parent = parent.parent;
            }
        }

        /// <summary>
        /// Returns the nearest controllers below the given transform. The walk stops at every
        /// container: a container is responsible for its own subtree, so descending past it
        /// here would claim its children twice.
        /// </summary>
        public static List<Controller> GetControllersInChildren(Transform parent)
        {
            List<Controller> result = new List<Controller>();
            CollectControllers(result, parent, includeSelf: false);
            return result;
        }

        private static void CollectControllers(List<Controller> result, Transform root, bool includeSelf)
        {
            Queue<Transform> pending = new Queue<Transform>();

            if (includeSelf)
            {
                pending.Enqueue(root);
            }
            else
            {
                foreach (Transform child in root)
                {
                    pending.Enqueue(child);
                }
            }

            while (pending.Count > 0)
            {
                Transform current = pending.Dequeue();

                ControllerContainer[] containers = current.GetComponents<ControllerContainer>();
                foreach (ControllerContainer container in containers)
                {
                    if (!result.Contains(container))
                    {
                        result.Add(container);
                    }
                }

                foreach (Controller controller in current.GetComponents<Controller>())
                {
                    if (!result.Contains(controller))
                    {
                        result.Add(controller);
                    }
                }

                // Stop here if this object is a container - it handles its own subtree.
                if (containers.Length > 0)
                {
                    continue;
                }

                foreach (Transform child in current)
                {
                    pending.Enqueue(child);
                }
            }
        }

        public static Controller GetParentController(Transform transform)
        {
            Transform current = transform;

            while (current != null)
            {
                Controller controller = current.GetComponent<Controller>();
                if (controller != null)
                {
                    return controller;
                }

                current = current.parent;
            }

            return null;
        }

        /// <summary>
        /// Tears down every controller in a scene. Called explicitly rather than relying on
        /// OnDestroy, so controllers that were never activated are cleaned up too.
        /// </summary>
        public static void DestroyAllControllersInScene(Scene scene)
        {
            foreach (Controller controller in FindRootControllers(scene))
            {
                controller.Destroy();
            }
        }
    }
}
