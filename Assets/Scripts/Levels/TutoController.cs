using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
public class TutoController : MonoBehaviour
{
    [Header("Ref")]
    public PlayerInput targetPlayerInput; 

    [Header("UI Elements")]
    public GameObject l2Sprite;
    public GameObject r2Sprite;
    public GameObject r1Sprite;
    public GameObject xSprite;
    public GameObject oSprite;

    public GameObject camionObject;
    public float truckSpeed = 3f;
    
    private InputAction _brakeAction;      // L2
    private InputAction _boostAction;      // L1
    private InputAction _driftAction;      // L2
    private InputAction _accelerateAction; // X
    private InputAction _jumpAction;       // O

    [SerializeField] private int currentStep = 0;

    void Start()
    {
        if (targetPlayerInput == null)
        {
            targetPlayerInput = FindAnyObjectByType<PlayerInput>();
        }
        _brakeAction = targetPlayerInput.actions["Brake"];
        _boostAction = targetPlayerInput.actions["Boost"];
        _accelerateAction = targetPlayerInput.actions["Accelerate"];
        _jumpAction = targetPlayerInput.actions["Jump"];
        _driftAction = targetPlayerInput.actions["Drift"];

        l2Sprite.SetActive(true);
        r2Sprite.SetActive(false);
        xSprite.SetActive(false);
        oSprite.SetActive(false);
    }

    void ClearAllSprites()
    {
        l2Sprite.SetActive(false);
        r2Sprite.SetActive(false);
        xSprite.SetActive(false);
        oSprite.SetActive(false);
        r1Sprite.SetActive(false);
    }

    void Update()
    {
        switch (currentStep)
        {
            case 0: // Reculade
                if (_brakeAction.WasPressedThisFrame())
                {
                    ClearAllSprites();
                    currentStep = 1;
                }
                break;

            case 1:
                break;

            case 2: // Acceleration
                if (_accelerateAction.WasPressedThisFrame())
                {
                    ClearAllSprites();
                    // wait for trig zone 2 
                }
                break;
            
            case 3: // Freinade
                if (_brakeAction.WasPressedThisFrame())
                {
                    ClearAllSprites();
                    StartCoroutine(MoveTruckRoutine());
                    oSprite.SetActive(true);
                    currentStep++;
                }
                break;
            case 4: // Jump
                if (oSprite.activeSelf == false) oSprite.SetActive(true);
                // wait for trig zone 
                break;
            case 5:
                ClearAllSprites();
                // wait for trig zone 
                break;
            case 6:
                r2Sprite.SetActive(true);
                currentStep++;
                break;
            case 7:
                if (_driftAction.WasPressedThisFrame())
                {
                    ClearAllSprites();
                }
                break;
            case 8:
                ClearAllSprites();
                r1Sprite.SetActive(true);
                currentStep++;
                break;
            case 9:
                if (_boostAction.WasPressedThisFrame())
                {
                    r1Sprite.SetActive(false);
                }
                break;
            case 10:
                ClearAllSprites();
                break;
        }
    }

    public void OnPlayerEnteredTrigger(int step)
    {
        if (step == 1)
        {
            xSprite.SetActive(true);
            l2Sprite.SetActive(false);
            currentStep=2;
            return;
        }
        
        if (step == 2 && currentStep == 2)
        {
            l2Sprite.SetActive(true);
            xSprite.SetActive(false);
            currentStep++;
            return;
        }
        currentStep = step;
    }
    
    IEnumerator MoveTruckRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        Vector3 startPos = camionObject.transform.position;
        Vector3 targetPos = startPos + (camionObject.transform.right * 5f);
        
        while (Vector3.Distance(camionObject.transform.position, targetPos) > 0.05f)
        {
            camionObject.transform.position = Vector3.MoveTowards(
                camionObject.transform.position, 
                targetPos, 
                truckSpeed * Time.deltaTime
            );
            yield return null;
        }
        camionObject.transform.position = targetPos;
    }
}