using UnityEngine;

namespace CozyFarm
{
    /// <summary>Keyboard movement and a fixed, gently following isometric camera.</summary>
    [DisallowMultipleComponent]
    public sealed class FarmPlayer : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float walkSpeed = 4.6f;
        [SerializeField, Min(1f)] private float runSpeed = 7f;
        [SerializeField, Range(7f, 20f)] private float cameraSize = 13f;

        private readonly Vector3 cameraOffset = new Vector3(13f, 19f, -17f);
        private CharacterController controller;
        private Camera followCamera;
        private Transform visual;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Material farmerMaterial;
        private Vector3 cameraVelocity;
        private float verticalSpeed;
        private float gait;
        private float stride;
        private bool initialized;

        public bool CanMove { get; set; } = true;

        public void Initialize(Camera camera)
        {
            if (initialized || camera == null) return;
            followCamera = camera;
            controller = GetComponent<CharacterController>();
            if (controller == null) controller = gameObject.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, 0.8f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 42f;
            controller.skinWidth = 0.04f;
            visual = transform.Find("FarmerVisual");
            if (visual == null) CreateFarmer();

            followCamera.orthographic = true;
            followCamera.orthographicSize = cameraSize;
            followCamera.nearClipPlane = 0.1f;
            followCamera.farClipPlane = 110f;
            followCamera.transform.rotation = Quaternion.LookRotation(-cameraOffset, Vector3.up);
            followCamera.transform.position = transform.position + Vector3.up * 0.5f + cameraOffset;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;

            Vector2 input = Vector2.zero;
            bool running = false;
            if (CanMove)
            {
                input.x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                input.y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                    - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                input = Vector2.ClampMagnitude(input, 1f);
                running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                cameraSize = Mathf.Clamp(cameraSize - Input.mouseScrollDelta.y * 0.8f, 8f, 18f);
            }

            Vector3 forward = followCamera.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = followCamera.transform.right;
            right.y = 0f;
            right.Normalize();
            Vector3 motion = (right * input.x + forward * input.y) * (running ? runSpeed : walkSpeed);
            Vector3 position = transform.position;
            float delta = Time.deltaTime;
            if (delta <= 0f) return;

            // Clamp the proposed horizontal movement before the controller resolves collisions.
            motion.x = (Mathf.Clamp(position.x + motion.x * delta, -20f, 20f) - position.x) / delta;
            motion.z = (Mathf.Clamp(position.z + motion.z * delta, -17f, 17f) - position.z) / delta;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed - 24f * delta, -35f);
            controller.Move(new Vector3(motion.x, verticalSpeed, motion.z) * delta);

            if (motion.sqrMagnitude > 0.02f && visual != null)
            {
                Quaternion heading = Quaternion.LookRotation(new Vector3(motion.x, 0f, motion.z));
                visual.rotation = Quaternion.Slerp(visual.rotation, heading, 14f * delta);
            }

            stride = Mathf.MoveTowards(stride, input.sqrMagnitude > 0f ? 1f : 0f, delta * 8f);
            gait += delta * (running ? 13f : 9f);
            AnimateLimb(leftLeg, Mathf.Sin(gait) * 24f * stride);
            AnimateLimb(rightLeg, -Mathf.Sin(gait) * 24f * stride);
            AnimateLimb(leftArm, -Mathf.Sin(gait) * 18f * stride);
            AnimateLimb(rightArm, Mathf.Sin(gait) * 18f * stride);
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            Vector3 target = transform.position + Vector3.up * 0.5f + cameraOffset;
            followCamera.transform.position = Vector3.SmoothDamp(
                followCamera.transform.position, target, ref cameraVelocity, 0.16f);
            followCamera.orthographicSize = Mathf.Lerp(
                followCamera.orthographicSize, cameraSize, 10f * Time.deltaTime);
        }

        private static void AnimateLimb(Transform limb, float angle)
        {
            if (limb != null) limb.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void CreateFarmer()
        {
            visual = new GameObject("FarmerVisual").transform;
            visual.SetParent(transform, false);
            farmerMaterial = new Material(Shader.Find("Standard"))
            {
                name = "Farmer shared material",
                enableInstancing = true
            };
            farmerMaterial.SetFloat("_Glossiness", 0f);
            Color skin = new Color(0.79f, 0.49f, 0.29f);
            Color shirt = new Color(0.95f, 0.80f, 0.43f);
            Color overalls = new Color(0.20f, 0.39f, 0.32f);
            Color boot = new Color(0.24f, 0.19f, 0.16f);
            Part("Torso", visual, new Vector3(0f, 0.94f, 0f), new Vector3(0.62f, 0.62f, 0.36f), shirt);
            Part("Overalls", visual, new Vector3(0f, 0.79f, 0.04f), new Vector3(0.54f, 0.40f, 0.39f), overalls);
            Part("Head", visual, new Vector3(0f, 1.45f, 0f), new Vector3(0.43f, 0.44f, 0.4f), skin);
            Part("HatBrim", visual, new Vector3(0f, 1.66f, 0f), new Vector3(0.78f, 0.08f, 0.66f), shirt);
            Part("HatCrown", visual, new Vector3(0f, 1.78f, 0f), new Vector3(0.48f, 0.20f, 0.43f), shirt);
            Part("HatBand", visual, new Vector3(0f, 1.70f, 0f), new Vector3(0.50f, 0.065f, 0.45f), boot);
            Part("Nose", visual, new Vector3(0f, 1.42f, 0.24f), new Vector3(0.12f, 0.12f, 0.13f), skin);
            leftLeg = Limb("LeftLeg", new Vector3(-0.17f, 0.6f, 0f), new Vector3(0.23f, 0.53f, 0.28f), overalls);
            rightLeg = Limb("RightLeg", new Vector3(0.17f, 0.6f, 0f), new Vector3(0.23f, 0.53f, 0.28f), overalls);
            Part("Boot", leftLeg, new Vector3(0f, -0.51f, 0.045f), new Vector3(0.26f, 0.17f, 0.37f), boot);
            Part("Boot", rightLeg, new Vector3(0f, -0.51f, 0.045f), new Vector3(0.26f, 0.17f, 0.37f), boot);
            leftArm = Limb("LeftArm", new Vector3(-0.41f, 1.17f, 0f), new Vector3(0.22f, 0.5f, 0.25f), skin);
            rightArm = Limb("RightArm", new Vector3(0.41f, 1.17f, 0f), new Vector3(0.22f, 0.5f, 0.25f), skin);
        }

        private Transform Limb(string name, Vector3 pivot, Vector3 scale, Color color)
        {
            Transform joint = new GameObject(name).transform;
            joint.SetParent(visual, false);
            joint.localPosition = pivot;
            Part("Mesh", joint, new Vector3(0f, -scale.y * 0.5f, 0f), scale, color);
            return joint;
        }

        private void Part(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = farmerMaterial;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private void OnDestroy()
        {
            if (farmerMaterial != null) Destroy(farmerMaterial);
        }
    }
}
