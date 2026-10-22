using UnityEngine;
using UnityEngine.InputSystem;

// Boisson main droite (R1) : une cristalline remplie de Red Bull apparaît dans la main droite,
// qui la monte devant soi puis la porte à la bouche. Tout est dans l'axe de la caméra.
// À vélo l'animation suit le boost (donc seulement s'il en reste), à pied on peut boire tout le temps.
public class DrinkGesture : MonoBehaviour
{
    [Header(" --- References --- ")]
    public Transform cameraTransform;
    [Tooltip("Racine du squelette de la main droite (Mains/Armature.002)")]
    public Transform rightArmature;
    [Tooltip("Os du poignet/paume (Bone.002)")]
    public Transform rightHandBone;
    public SkinnedMeshRenderer rightHandRenderer;
    public GameObject bottlePrefab;
    [Tooltip("À vélo : on boit pendant le boost. Vide à pied : on boit avec l'action Boire")]
    public BikeControlsOW bikeControls;
    [Tooltip("À pied : boire rend bourré")]
    public DrunkManager drunkManager;

    [Header(" --- Bouteille --- ")]
    [Tooltip("Échelle de la cristalline (le mesh fait ~1,9 m de haut)")]
    public float bottleScale = 0.11f;
    [Tooltip("Décalage de la bouteille dans le poing (repère bouteille, mètres)")]
    public Vector3 gripOffset = Vector3.zero;
    [Tooltip("Rotation de la bouteille dans le poing (Z = tourne la main autour de la bouteille)")]
    public Vector3 gripEuler = new Vector3(0f, 0f, -150f);

    [Header(" --- Poses de la bouteille (repère caméra) --- ")]
    [Tooltip("Bouteille tenue devant soi (étape intermédiaire)")]
    public Vector3 holdPosition = new Vector3(0.16f, -0.14f, 0.42f);
    [Tooltip("Rotation de la bouteille tenue (0 = debout, bouchon en haut)")]
    public Vector3 holdEuler = new Vector3(-10f, 0f, 10f);
    [Tooltip("Bouteille à la bouche")]
    public Vector3 mouthPosition = new Vector3(0.11f, -0.06f, 0.21f);
    [Tooltip("Rotation de la bouteille à la bouche (bouchon vers la bouche, cul vers le haut)")]
    public Vector3 mouthEuler = new Vector3(-112f, 0f, 30f);
    [Tooltip("Amplitude (degrés) du mouvement de gorgée")]
    public float sipAmplitude = 4f;
    public float sipFrequency = 7f;

    [Header(" --- Sans guidon (joueur à pied) --- ")]
    public bool hiddenWhenIdle;
    public Vector3 hiddenDropOffset = new Vector3(0f, -0.5f, -0.1f);
    [Tooltip("Ivresse gagnée par seconde passée à boire (à pied)")]
    public float drunkPerSecond = 0.15f;

    [Header(" --- Animation --- ")]
    [Tooltip("Vitesse de l'animation (1 = une seconde du guidon à la bouche)")]
    public float raiseSpeed = 2.5f;
    public float lowerSpeed = 3f;

    [Header(" --- Debug --- ")]
    public bool forceGesture;

    private FpsHandRig _rig;
    private InputAction _boireAction;
    private Transform _bottle;
    private Matrix4x4 _bottleInHand;
    private Quaternion _uprightRotation;
    private float _weight;

    private void Awake()
    {
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null) _boireAction = playerInput.actions["Boire"];
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        _rig = new FpsHandRig(rightArmature, rightHandBone, null, null);
        if (rightHandRenderer != null) rightHandRenderer.updateWhenOffscreen = true;

        // Bouteille debout dans le repère caméra : le bouchon (axe Z du mesh) vers le haut
        _uprightRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);

        _bottle = Instantiate(bottlePrefab, transform).transform;
        _bottle.localScale = Vector3.one * bottleScale;
        _bottle.gameObject.SetActive(false);
        _bottleInHand = ComputeBottleInHand();
    }

    // Place la bouteille dans le poing fermé (pose du guidon) : axe auriculaire -> index, bouchon côté pouce,
    // centrée sur le cercle formé par les doigts repliés
    private Matrix4x4 ComputeBottleInHand()
    {
        Transform pinky = FpsHandRig.FindDeep(rightHandBone, "Bone.003");
        Transform index = FpsHandRig.FindDeep(rightHandBone, "Bone.012");
        Vector3 axis = (index.position - pinky.position).normalized;
        Vector3 origin = (index.position + pinky.position) * 0.5f;
        Vector3 up = Vector3.ProjectOnPlane(origin - rightHandBone.position, axis).normalized;
        Vector3 right = Vector3.Cross(up, axis);

        string[] fingerBones =
        {
            "Bone.003", "Bone.004", "Bone.005", "Bone.005_end", "Bone.006", "Bone.007", "Bone.008", "Bone.008_end",
            "Bone.009", "Bone.010", "Bone.011", "Bone.011_end", "Bone.012", "Bone.013", "Bone.014", "Bone.014_end"
        };
        Vector2[] points = new Vector2[fingerBones.Length];
        for (int i = 0; i < fingerBones.Length; i++)
        {
            Vector3 d = FpsHandRig.FindDeep(rightHandBone, fingerBones[i]).position - origin;
            points[i] = new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, up));
        }
        Vector2 center = FitCircleCenter(points);

        Quaternion rot = Quaternion.LookRotation(axis, up) * Quaternion.Euler(gripEuler);
        Vector3 pos = origin + right * center.x + up * center.y + rot * gripOffset;

        Matrix4x4 bottleWorld = Matrix4x4.TRS(pos, rot, Vector3.one * bottleScale);
        return rightHandBone.localToWorldMatrix.inverse * bottleWorld;
    }

    // Cercle des moindres carrés (méthode de Kåsa) : x² + y² + a.x + b.y + c = 0
    private static Vector2 FitCircleCenter(Vector2[] points)
    {
        double sxx = 0, sxy = 0, syy = 0, sx = 0, sy = 0, sxz = 0, syz = 0, sz = 0;
        foreach (Vector2 p in points)
        {
            double x = p.x, y = p.y, z = x * x + y * y;
            sxx += x * x; sxy += x * y; syy += y * y; sx += x; sy += y;
            sxz += x * z; syz += y * z; sz += z;
        }
        Matrix4x4 m = Matrix4x4.identity;
        m.SetRow(0, new Vector4((float)sxx, (float)sxy, (float)sx, 0f));
        m.SetRow(1, new Vector4((float)sxy, (float)syy, (float)sy, 0f));
        m.SetRow(2, new Vector4((float)sx, (float)sy, points.Length, 0f));
        Vector3 abc = m.inverse.MultiplyVector(new Vector3((float)-sxz, (float)-syz, (float)-sz));
        return new Vector2(-abc.x * 0.5f, -abc.y * 0.5f);
    }

    private void LateUpdate()
    {
        bool active = forceGesture || IsDrinking();
        _weight = Mathf.MoveTowards(_weight, active ? 1f : 0f, (active ? raiseSpeed : lowerSpeed) * Time.deltaTime);
        float w = Mathf.SmoothStep(0f, 1f, _weight);

        bool visible = w > 0.01f;
        _bottle.gameObject.SetActive(visible);
        if (hiddenWhenIdle && rightHandRenderer != null) rightHandRenderer.enabled = visible;

        // Petite gorgée quand la bouteille est à la bouche
        Vector3 sip = new Vector3(Mathf.Sin(Time.time * sipFrequency) * sipAmplitude, 0f, 0f) * Mathf.InverseLerp(0.8f, 1f, w);
        PoseForBottle(holdPosition, holdEuler, out Vector3 holdPos, out Quaternion holdRot);
        PoseForBottle(mouthPosition, mouthEuler + sip, out Vector3 mouthPos, out Quaternion mouthRot);

        Vector3 restPos;
        Quaternion restRot;
        if (hiddenWhenIdle)
            PoseForBottle(holdPosition + hiddenDropOffset, holdEuler, out restPos, out restRot);
        else
            _rig.GetRestPose(out restPos, out restRot);

        // Guidon -> tenue devant soi -> bouche
        float toHold = Mathf.Clamp01(w * 2f);
        float toMouth = Mathf.Clamp01(w * 2f - 1f);
        Vector3 pos = Vector3.Lerp(Vector3.Lerp(restPos, holdPos, toHold), mouthPos, toMouth);
        Quaternion rot = Quaternion.Slerp(Quaternion.Slerp(restRot, holdRot, toHold), mouthRot, toMouth);
        _rig.Apply(pos, rot, 0f);

        if (visible)
        {
            Matrix4x4 bottleWorld = rightHandBone.localToWorldMatrix * _bottleInHand;
            _bottle.SetPositionAndRotation(bottleWorld.GetColumn(3), Quaternion.LookRotation(bottleWorld.GetColumn(2), bottleWorld.GetColumn(1)));
        }

        // À pied, chaque seconde à la bouche rend un peu plus bourré (à vélo c'est le boost qui s'en charge)
        if (bikeControls == null && drunkManager != null && active && w > 0.95f)
            drunkManager.AddDrunk(drunkPerSecond * Time.deltaTime);
    }

    private bool IsDrinking()
    {
        if (bikeControls != null) return bikeControls.IsBoosting();
        return _boireAction != null && _boireAction.IsPressed();
    }

    // Pose de la racine du squelette pour que la bouteille soit à cette pose (repère caméra)
    private void PoseForBottle(Vector3 position, Vector3 euler, out Vector3 localPos, out Quaternion localRot)
    {
        Quaternion bottleRot = cameraTransform.rotation * Quaternion.Euler(euler) * _uprightRotation;
        Matrix4x4 bottleWorld = Matrix4x4.TRS(cameraTransform.TransformPoint(position), bottleRot, Vector3.one * bottleScale);
        _rig.GetPoseForHand(bottleWorld * _bottleInHand.inverse, out localPos, out localRot);
    }
}
