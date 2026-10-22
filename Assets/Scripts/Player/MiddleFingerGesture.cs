using UnityEngine;
using UnityEngine.InputSystem;

// Doigt d'honneur main gauche (L1) : la main quitte sa pose de repos (guidon, ou sous l'écran à pied)
// et vient se placer devant la caméra, dans son axe (elle suit le regard), majeur tendu et autres doigts repliés.
public class MiddleFingerGesture : MonoBehaviour
{
    [System.Serializable]
    public struct FingerPose
    {
        public string boneName;
        public Vector3 targetEuler;
    }

    [Header(" --- References --- ")]
    public Transform cameraTransform;
    [Tooltip("Racine du squelette de la main gauche (Mains/Armature)")]
    public Transform leftArmature;
    [Tooltip("Os du poignet/paume (Bone.002) : son axe Y pointe vers les doigts")]
    public Transform leftHandBone;
    public SkinnedMeshRenderer leftHandRenderer;

    [Header(" --- Placement (repère caméra) --- ")]
    [Tooltip("Position de la paume par rapport à la caméra (x = droite, y = haut, z = devant)")]
    public Vector3 handOffset = new Vector3(-0.24f, -0.12f, 0.45f);
    [Tooltip("Rotation de la main par rapport à la caméra")]
    public Vector3 handEulerOffset = new Vector3(22f, 10f, -22f);
    [Tooltip("Rotation locale du poignet pendant le geste (redresse la main dans l'axe de l'avant-bras)")]
    public Vector3 wristTargetEuler = Vector3.zero;

    [Header(" --- Sans guidon (joueur à pied) --- ")]
    [Tooltip("Main invisible au repos : elle monte depuis le bas de l'écran pendant le geste")]
    public bool hiddenWhenIdle;
    [Tooltip("Décalage (repère caméra) de la pose cachée par rapport à la pose du geste")]
    public Vector3 hiddenDropOffset = new Vector3(0f, -0.5f, -0.1f);

    [Header(" --- Animation --- ")]
    public float raiseSpeed = 7f;
    public float lowerSpeed = 5f;

    [Header(" --- Doigts (rotation locale cible) --- ")]
    public FingerPose[] fingerPoses =
    {
        // Majeur tendu
        new FingerPose { boneName = "Bone.009", targetEuler = new Vector3(-5f, 0f, 0f) },
        new FingerPose { boneName = "Bone.010", targetEuler = new Vector3(-3f, 0f, 0f) },
        new FingerPose { boneName = "Bone.011", targetEuler = new Vector3(-3f, 0f, 0f) },
        // Index replié
        new FingerPose { boneName = "Bone.012", targetEuler = new Vector3(-85f, 8f, 8f) },
        new FingerPose { boneName = "Bone.013", targetEuler = new Vector3(-95f, 0f, 0f) },
        new FingerPose { boneName = "Bone.014", targetEuler = new Vector3(-70f, 0f, 0f) },
        // Annulaire replié
        new FingerPose { boneName = "Bone.006", targetEuler = new Vector3(-85f, 0f, 0f) },
        new FingerPose { boneName = "Bone.007", targetEuler = new Vector3(-95f, 0f, 0f) },
        new FingerPose { boneName = "Bone.008", targetEuler = new Vector3(-70f, 0f, 0f) },
        // Auriculaire replié
        new FingerPose { boneName = "Bone.003", targetEuler = new Vector3(-85f, 0f, -8f) },
        new FingerPose { boneName = "Bone.004", targetEuler = new Vector3(-95f, 0f, 0f) },
        new FingerPose { boneName = "Bone.005", targetEuler = new Vector3(-70f, 0f, 0f) },
        // Pouce rabattu sur l'index
        new FingerPose { boneName = "Bone.015", targetEuler = new Vector3(-33f, -79f, 55f) },
        new FingerPose { boneName = "Bone.016", targetEuler = new Vector3(-27f, 21f, -60f) },
    };

    [Header(" --- Debug --- ")]
    public bool forceGesture;

    private Transform[] _fingerBones;
    private Quaternion[] _fingerRestRotations;
    private Quaternion[] _fingerTargetRotations;
    private Vector3 _armatureRestLocalPos;
    private Quaternion _armatureRestLocalRot;
    private Vector3 _handPosInArmature;
    private Quaternion _handRotInArmature;
    private Quaternion _wristRestRotation;
    private Quaternion _wristTargetRotation;
    private float _weight;
    private InputAction _doigtAction;

    private void Awake()
    {
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null) _doigtAction = playerInput.actions["DoigtHonneur"];
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        _armatureRestLocalPos = leftArmature.localPosition;
        _armatureRestLocalRot = leftArmature.localRotation;
        _wristRestRotation = leftHandBone.localRotation;
        _wristTargetRotation = Quaternion.Euler(wristTargetEuler);
        _handPosInArmature = leftArmature.InverseTransformPoint(leftHandBone.position);
        // Orientation de la paume dans le squelette une fois le poignet redressé
        _handRotInArmature = Quaternion.Inverse(leftArmature.rotation) * leftHandBone.parent.rotation * _wristTargetRotation;

        _fingerBones = new Transform[fingerPoses.Length];
        _fingerRestRotations = new Quaternion[fingerPoses.Length];
        _fingerTargetRotations = new Quaternion[fingerPoses.Length];
        for (int i = 0; i < fingerPoses.Length; i++)
        {
            _fingerBones[i] = FindDeep(leftHandBone, fingerPoses[i].boneName);
            if (_fingerBones[i] == null) continue;
            _fingerRestRotations[i] = _fingerBones[i].localRotation;
            _fingerTargetRotations[i] = Quaternion.Euler(fingerPoses[i].targetEuler);
        }

        // La main sort de la zone du guidon : on évite qu'Unity la culle avec les bounds d'origine
        if (leftHandRenderer != null) leftHandRenderer.updateWhenOffscreen = true;
    }

    private void LateUpdate()
    {
        bool active = forceGesture || (_doigtAction != null && _doigtAction.IsPressed());
        _weight = Mathf.MoveTowards(_weight, active ? 1f : 0f, (active ? raiseSpeed : lowerSpeed) * Time.deltaTime);
        float w = Mathf.SmoothStep(0f, 1f, _weight);

        if (hiddenWhenIdle && leftHandRenderer != null) leftHandRenderer.enabled = w > 0f;

        // Pose devant la caméra : on place la paume, puis on en déduit la racine du squelette
        Quaternion gestureRot = cameraTransform.rotation * Quaternion.Euler(handEulerOffset) * Quaternion.Inverse(_handRotInArmature);
        Vector3 gesturePos = ArmaturePosForHand(cameraTransform.TransformPoint(handOffset), gestureRot);

        // Pose de repos : sur le guidon (recalculée chaque frame car le vélo bouge) ou cachée sous l'écran
        Vector3 restPos;
        Quaternion restRot;
        if (hiddenWhenIdle)
        {
            restRot = gestureRot;
            restPos = ArmaturePosForHand(cameraTransform.TransformPoint(handOffset + hiddenDropOffset), gestureRot);
        }
        else
        {
            Transform parent = leftArmature.parent;
            restPos = parent.TransformPoint(_armatureRestLocalPos);
            restRot = parent.rotation * _armatureRestLocalRot;
        }

        if (w <= 0f)
        {
            leftArmature.SetPositionAndRotation(restPos, restRot);
            ApplyFingers(0f);
            return;
        }

        leftArmature.SetPositionAndRotation(Vector3.Lerp(restPos, gesturePos, w), Quaternion.Slerp(restRot, gestureRot, w));
        ApplyFingers(w);
    }

    private Vector3 ArmaturePosForHand(Vector3 handPos, Quaternion armatureRot)
    {
        return handPos - armatureRot * Vector3.Scale(_handPosInArmature, leftArmature.lossyScale);
    }

    private void ApplyFingers(float w)
    {
        leftHandBone.localRotation = Quaternion.Slerp(_wristRestRotation, _wristTargetRotation, w);
        for (int i = 0; i < _fingerBones.Length; i++)
        {
            if (_fingerBones[i] == null) continue;
            _fingerBones[i].localRotation = Quaternion.Slerp(_fingerRestRotations[i], _fingerTargetRotations[i], w);
        }
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, boneName);
            if (found != null) return found;
        }
        return null;
    }
}
