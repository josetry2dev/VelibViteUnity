using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCollision : MonoBehaviour
{
    [Header("References")]
    public BikeControlsBase bikeControls; 
    public BikeHealthSystem bikeHealthSystem;

    [Header("Settings")]
    public float recoilForce = 20f; // bonk force

    private bool _hasFinished;

    public void SetFinished()
    {
        _hasFinished = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_hasFinished) return;
        
        if (collision.gameObject.CompareTag("Obstacle")
            || collision.gameObject.CompareTag("Car")
            || collision.gameObject.CompareTag("Pieton"))
        {
            if (bikeControls is BikeControlsRunner) bikeHealthSystem.TakeDamage();
            
            else
            {
                // --- CAS 1 : GROS CRASH ---
                if (collision.gameObject.CompareTag("Obstacle")
                    && (bikeControls.GetSpeedForUI() > 10))
                {
                    if (collision.gameObject.layer == LayerMask.NameToLayer("Floor"))
                    {
                        Vector3 pushDir = collision.GetContact(0).normal;
                        pushDir = pushDir.normalized;
                        if (pushDir.y >0.95f) // Si on atterit sur le sol apres un saut 
                        {
                            return;
                        }
                    }
                    Rumble(1f, 0.2f, 0.4f);
                    bikeHealthSystem.TakeDamage();
                    return;
                }
                else if (collision.gameObject.CompareTag("Car"))
                {
                    var behavior = collision.gameObject.GetComponent<CarBehavior>();
                    if (behavior.GetSpeed() > 5 ||
                        (bikeControls.GetSpeedForUI() > 10))
                    {
                        Rumble(1f, 0.2f, 0.4f);
                        bikeHealthSystem.TakeDamage();
                        return;
                    }
                }
                else if (collision.gameObject.CompareTag("Pieton")
                         && (bikeControls.GetSpeedForUI() > 5))
                {
                    Rumble(1f, 0.2f, 0.4f);
                    bikeHealthSystem.TakeDamage();
                    return;
                }
                
                Rumble(0f, 0.8f, 0.1f);
                
                Vector3 pushDir2 = collision.GetContact(0).normal;

                pushDir2.y = Mathf.Max(pushDir2.y, 0.5f); 
                pushDir2 = pushDir2.normalized;

                if ((bikeControls is BikeControlsOW owScript))
                {
                    if (collision.gameObject.layer == LayerMask.NameToLayer("Floor"))
                    {
                        if (pushDir2.y >0.95f)
                        {
                            return;
                        }
                        owScript.StepUp(); // On monte un trottoir
                    }
                    else
                    {
                        owScript.ApplyBonk(pushDir2 * recoilForce);
                    }
                }
                
            }
            
        }
    }
    
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
}