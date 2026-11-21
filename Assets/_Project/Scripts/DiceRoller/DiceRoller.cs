
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
    [Tooltip("Roll when the dice is clicked. Combat turns this off and rolls it from code.")]
    [SerializeField] bool clickToRoll = true;

    [Header("UI References")]
    [SerializeField] TMPro.TextMeshProUGUI resultText;

    // Fired with the number on top when a roll finishes
    public event System.Action<int> OnRolled;

    public bool ClickToRoll { get => clickToRoll; set => clickToRoll = value; }

    DiceSides diceSides;
    Rigidbody rb;

    CountdownTimer rollTimer;

    Vector3 originPosition;
    bool finalize;

    void Awake() {
        diceSides = GetComponent<DiceSides>();
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
            FinalizeRoll();
        }
    }

    void OnCollisionEnter(Collision col) {
        if (rollTimer.IsRunning && rollTimer.Progress < 0.5f && rb.angularVelocity.magnitude < minAngularVelocity) {
            finalize = true;
        }
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
    }

    void FinalizeRoll() {
        rollTimer.Stop();
        finalize = false;
        ResetDiceState();

        int result = diceSides.GetMatch();
        Debug.Log($"Dice landed on {result}");
        resultText.text = result.ToString();
        OnRolled?.Invoke(result);
    }

    void ResetDiceState() {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
