
using UnityEngine;
using Utilities;
using Random = UnityEngine.Random;

[RequireComponent(typeof(DiceSides))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(AudioSource))]
public class DiceRoller : MonoBehaviour {
    [Header("Dice Rolling Settings")]
    [SerializeField] float rollForce = 50f;
    [SerializeField] float torqueAmount = 5f;
    [SerializeField] float maxRollTime = 3f;
    [SerializeField] float minAngularVelocity = 0.1f;
    [SerializeField] float smoothTime = 0.1f;
    [SerializeField] float maxSpeed = 15f;
    [Tooltip("Roll when the dice is clicked. Combat turns this off and rolls it from code.")]
    [SerializeField] bool clickToRoll = true;
    
    [Header("UI References")]
    [SerializeField] TMPro.TextMeshProUGUI resultText;

    //[Header("Audio & Particle Effects")]
    //[SerializeField] AudioClip shakeClip;
    //[SerializeField] AudioClip rollClip;
    //[SerializeField] AudioClip impactClip;
    //[SerializeField] AudioClip finalResultClip;
    //[SerializeField] GameObject impactEffect;
    //[SerializeField] GameObject finalResultEffect;

    // Fired with the number on top when a roll finishes
    public event System.Action<int> OnRolled;

    public bool IsRolling => rollTimer != null && (rollTimer.IsRunning || finalize);
    public bool ClickToRoll { get => clickToRoll; set => clickToRoll = value; }
    public TMPro.TextMeshProUGUI ResultText => resultText;

    DiceSides diceSides;
    //AudioSource audioSource;
    Rigidbody rb;

    CountdownTimer rollTimer;
    
    Vector3 originPosition;
    Vector3 currentVelocity;
    bool finalize;

    void Awake() {
        diceSides = GetComponent<DiceSides>();
        //audioSource = GetComponent<AudioSource>();
        rb = GetComponent<Rigidbody>();

        resultText.text = "Click to roll";
        originPosition = transform.position;
        
        rollTimer = new CountdownTimer(maxRollTime);
        rollTimer.OnTimerStart += PerformInitialRoll;
        rollTimer.OnTimerStop += () => finalize = true;
    }

    void OnMouseUp() {
        if (!clickToRoll) return;
        Roll();
    }

    // Start a roll from code (e.g. an attack). Listen to OnRolled for the result.
    public void Roll() {
        if (rollTimer == null || rollTimer.IsRunning) return;
        rollTimer.Start();
    }

    void Update() {
        rollTimer.Tick(Time.deltaTime);

        if (finalize) {
           // MoveDiceToCenter();
           FinalizeRoll();
        }
    }

    void OnCollisionEnter(Collision col) {
        if (rollTimer.IsRunning && rollTimer.Progress < 0.5f && rb.angularVelocity.magnitude < minAngularVelocity) {
            finalize = true;
        }
        
        //audioSource.PlayOneShot(impactClip);
        //var particles = InstantiateFX(impactEffect, col.contacts[0].point, 1f);
        //Destroy(particles, 1f);
    }

    void PerformInitialRoll() {
        ResetDiceState();
        resultText.text = "";

        // Throw from the middle of the tray with a random spin, so every roll looks different
        transform.position = originPosition;
        transform.rotation = Random.rotation;
        
        Vector3 targetPosition = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        rb.AddForce(targetPosition * rollForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * torqueAmount, ForceMode.Impulse);
        
        //audioSource.clip = shakeClip;
        //audioSource.loop = true;
        //audioSource.Play();
    }

    void MoveDiceToCenter() {
        transform.position = Vector3.SmoothDamp(transform.position, originPosition, ref currentVelocity, smoothTime, maxSpeed);

        if (originPosition.InRangeOf(transform.position, 0.1f)) {
            FinalizeRoll();
        }
    }

    void FinalizeRoll() {
        rollTimer.Stop();
        finalize = false;
        ResetDiceState();
        
        //audioSource.loop = false;
        //audioSource.Stop();
        //audioSource.PlayOneShot(finalResultClip);
        
        //var particles = InstantiateFX(finalResultEffect, transform.position, 5f);
        //Destroy(particles, 3f);
        
        int result = diceSides.GetMatch();
        Debug.Log($"Dice landed on {result}");
        resultText.text = result.ToString();
        OnRolled?.Invoke(result);
    }

    void ResetDiceState() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        //transform.position = originPosition;
    }
    
    GameObject InstantiateFX(GameObject fx, Vector3 position, float size) {
        var particles = Instantiate(fx, position, Quaternion.identity);
        particles.transform.localScale = Vector3.one * size;
        return particles;
    }
}