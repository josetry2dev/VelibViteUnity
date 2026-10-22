using UnityEngine;

// Pilote une main riggée (Mains/Armature) : on donne la pose voulue de l'os de la paume en world,
// et on en déduit la pose de la racine du squelette. Gère aussi les squelettes en miroir (scale négatif).
public class FpsHandRig
{
    [System.Serializable]
    public struct FingerPose
    {
        public string boneName;
        public Vector3 targetEuler;
    }

    public readonly Transform Armature;
    public readonly Transform HandBone;

    private readonly Vector3 _restLocalPos;
    private readonly Quaternion _restLocalRot;
    private readonly Quaternion _wristRestRotation;
    private readonly Quaternion _wristTargetRotation;
    private readonly Matrix4x4 _handInArmature;
    private readonly Transform[] _fingerBones;
    private readonly Quaternion[] _fingerRestRotations;
    private readonly Quaternion[] _fingerTargetRotations;

    public FpsHandRig(Transform armature, Transform handBone, Quaternion? wristTarget, FingerPose[] fingerPoses)
    {
        Armature = armature;
        HandBone = handBone;
        _restLocalPos = armature.localPosition;
        _restLocalRot = armature.localRotation;
        _wristRestRotation = handBone.localRotation;
        _wristTargetRotation = wristTarget ?? _wristRestRotation;

        // Pose de la paume dans le squelette, une fois le poignet dans sa pose de geste
        handBone.localRotation = _wristTargetRotation;
        _handInArmature = armature.worldToLocalMatrix * handBone.localToWorldMatrix;
        handBone.localRotation = _wristRestRotation;

        int count = fingerPoses != null ? fingerPoses.Length : 0;
        _fingerBones = new Transform[count];
        _fingerRestRotations = new Quaternion[count];
        _fingerTargetRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            _fingerBones[i] = FindDeep(handBone, fingerPoses[i].boneName);
            if (_fingerBones[i] == null) continue;
            _fingerRestRotations[i] = _fingerBones[i].localRotation;
            _fingerTargetRotations[i] = Quaternion.Euler(fingerPoses[i].targetEuler);
        }
    }

    // Pose de repos (sur le guidon), dans le repère du parent
    public void GetRestPose(out Vector3 localPos, out Quaternion localRot)
    {
        localPos = _restLocalPos;
        localRot = _restLocalRot;
    }

    // Pose de la racine (repère parent) pour que la paume ait la matrice world donnée
    public void GetPoseForHand(Matrix4x4 handWorld, out Vector3 localPos, out Quaternion localRot)
    {
        Matrix4x4 local = Armature.parent.worldToLocalMatrix * handWorld * _handInArmature.inverse;
        Vector3 s = Armature.localScale;
        localPos = local.GetColumn(3);
        localRot = Quaternion.LookRotation(local.GetColumn(2) / s.z, local.GetColumn(1) / s.y);
    }

    // Matrice world de la paume pour une main gauche placée devant la caméra
    public Matrix4x4 HandWorldInCamera(Transform cam, Vector3 offset, Vector3 euler)
    {
        float handScale = HandBone.lossyScale.x;
        return Matrix4x4.TRS(cam.position, cam.rotation, Vector3.one) * Matrix4x4.TRS(offset, Quaternion.Euler(euler), Vector3.one * handScale);
    }

    public void Apply(Vector3 localPos, Quaternion localRot, float fingerWeight)
    {
        Armature.SetLocalPositionAndRotation(localPos, localRot);
        HandBone.localRotation = Quaternion.Slerp(_wristRestRotation, _wristTargetRotation, fingerWeight);
        for (int i = 0; i < _fingerBones.Length; i++)
        {
            if (_fingerBones[i] == null) continue;
            _fingerBones[i].localRotation = Quaternion.Slerp(_fingerRestRotations[i], _fingerTargetRotations[i], fingerWeight);
        }
    }

    public static Transform FindDeep(Transform root, string boneName)
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
