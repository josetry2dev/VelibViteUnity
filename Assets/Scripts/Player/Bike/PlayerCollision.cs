using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

// Résolution des chocs du vélo : gros crash (perte de vie), bonk (recul) ou marche de trottoir
public class PlayerCollision : MonoBehaviour
{
    private const string ObstacleTag = "Obstacle";
    private const string CarTag = "Car";
    private const string PietonTag = "Pieton";

    [Header(" --- References --- ")]
    public BikeControlsBase bikeControls;
    public BikeHealthSystem bikeHealthSystem;

    [Header(" --- Bonk Settings --- ")]
    public float recoilForce = 20f; // bonk force

    [Header(" --- Crash Thresholds (vitesse UI du vélo) --- ")]
    [Tooltip("Au-dessus, un obstacle ou une voiture = crash")]
    public float obstacleCrashSpeed = 10f;
    [Tooltip("Au-dessus, un piéton = crash")]
    public float pietonCrashSpeed = 5f;
    [Tooltip("Une voiture plus rapide que ça = crash, même si on roule doucement")]
    public float carCrashSpeed = 5f;

    private bool _hasFinished;
    private int _floorLayer;

    void Awake()
    {
        _floorLayer = LayerMask.NameToLayer("Floor");
    }

    public void SetFinished()
    {
        _hasFinished = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasFinished) return;

        GameObject other = collision.gameObject;
        bool isObstacle = other.CompareTag(ObstacleTag);
        bool isCar = other.CompareTag(CarTag);
        bool isPieton = other.CompareTag(PietonTag);
        if (!isObstacle && !isCar && !isPieton) return;

        Vector3 normal = collision.GetContact(0).normal;
        bool isFloor = other.layer == _floorLayer;
        if (isFloor && normal.y > 0.95f) return; // Atterrissage sur le sol après un saut

        // --- CAS 1 : GROS CRASH ---
        if (IsCrash(other, isObstacle, isCar))
        {
            Rumble(1f, 0.2f, 0.4f);
            bikeHealthSystem.TakeDamage();
            return;
        }

        // --- CAS 2 : BONK / TROTTOIR ---
        Rumble(0f, 0.8f, 0.1f);
        if (!(bikeControls is BikeControlsOW owScript)) return;

        if (isFloor)
        {
            owScript.StepUp(); // On monte un trottoir
        }
        else
        {
            normal.y = Mathf.Max(normal.y, 0.5f);
            owScript.ApplyBonk(normal.normalized * recoilForce);
        }
    }

    private bool IsCrash(GameObject other, bool isObstacle, bool isCar)
    {
        float bikeSpeed = bikeControls.GetSpeedForUI();
        if (isObstacle) return bikeSpeed > obstacleCrashSpeed;
        if (isCar)
        {
            var car = other.GetComponent<CarBehavior>();
            return bikeSpeed > obstacleCrashSpeed || (car != null && car.GetSpeed() > carCrashSpeed);
        }
        return bikeSpeed > pietonCrashSpeed;
    }

    #region Rumble
    public void Rumble(float low, float high, float duration)
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(low, high);
            StartCoroutine(StopRumble(duration));
        }
    }
    private IEnumerator StopRumble(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
    void OnDisable()
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
    #endregion
}
