using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    private enum Step { ShowTooth, RotateToView, OpenMenu, ShowCusps, SelectCusp, Done }

    [Header("Instruction")]
    [SerializeField] private TMP_Text instructionsText;

    [Header("Step 1: Show the tooth")]
    [SerializeField] private Button showToothButton;
    [FormerlySerializedAs("practiceCube")]
    [SerializeField] private GameObject practiceObject;
    [SerializeField] private XRGrabInteractable practiceGrab;

    [Header("Step 2: Rotate to a view")]
    [SerializeField] private ToothViewTracker viewTracker;
    [SerializeField] private DragToRotate rotator;
    [SerializeField] private ToothView targetView = ToothView.Occlusal;

    [Header("Steps 3-5: Menu and cusps")]
    [SerializeField] private SummonedMenu settingsMenu;
    [SerializeField] private AnatomyHotspotManager hotspotManager;

    [Header("Controller Hints")]
    [SerializeField] private GameObject[] indexTriggerHints;
    [SerializeField] private GameObject[] gripHints;
    [SerializeField] private GameObject[] joystickHints;
    [SerializeField] private GameObject[] menuButtonHints;   // optional

    [Header("Controller Guide Fade")]
    [SerializeField] private Renderer[] controllerGuideRenderers;
    [SerializeField] private Material fadedControllerMaterial;

    [Header("End")]
    [SerializeField] private Button enterStudyButton;
    [SerializeField] private string studyScene = "StudyMode_AR";

    private Step step;

    private void Awake()
    {
        showToothButton.onClick.AddListener(OnShowToothClicked);
        enterStudyButton.onClick.AddListener(EnterStudyMode);
        if (practiceGrab != null) practiceGrab.selectEntered.AddListener(OnGrabbed);
        if (practiceGrab != null)
        {
            practiceGrab.selectEntered.AddListener(OnGrabbed);
            practiceGrab.selectExited.AddListener(OnReleased);
        }
    }

    private void OnDestroy()
    {
        if (showToothButton != null) showToothButton.onClick.RemoveListener(OnShowToothClicked);
        if (enterStudyButton != null) enterStudyButton.onClick.RemoveListener(EnterStudyMode);
        if (practiceGrab != null) practiceGrab.selectEntered.RemoveListener(OnGrabbed);
        if (practiceGrab != null)
        {
            practiceGrab.selectEntered.RemoveListener(OnGrabbed);
            practiceGrab.selectExited.RemoveListener(OnReleased);
        }
    }

    private void Start()
    {
        practiceObject.SetActive(false);
        showToothButton.gameObject.SetActive(true);
        enterStudyButton.gameObject.SetActive(false);

        // Start with every structure hidden; the user turns cusps on in step 4.
        if (hotspotManager != null)
        {
            hotspotManager.SetCuspFilter(false);
            hotspotManager.SetRidgeFilter(false);
            hotspotManager.SetPitFilter(false);
            hotspotManager.SetFossaFilter(false);
            hotspotManager.SetGrooveFilter(false);
        }

        GoTo(Step.ShowTooth);
    }

    // Steps that wait on a state rather than an event are checked here.
    private void Update()
    {
        switch (step)
        {
            case Step.RotateToView:
                    bool released = rotator == null || !rotator.IsMoving;
                    if (viewTracker != null && viewTracker.CurrentView == targetView && released)
                    GoTo(Step.OpenMenu);
                break;

            case Step.OpenMenu:
                if (settingsMenu != null && settingsMenu.IsOpen) GoTo(Step.ShowCusps);
                break;

            case Step.ShowCusps:
                if (hotspotManager != null && hotspotManager.showCusps) GoTo(Step.SelectCusp);
                break;

            case Step.SelectCusp:
                AnatomyRegion selected = hotspotManager != null ? hotspotManager.SelectedRegion : null;
                if (selected != null && selected.structureType == AnatomyStructureType.Cusp)
                    GoTo(Step.Done);
                break;
        }
    }

    private void OnShowToothClicked()
    {
        if (step != Step.ShowTooth) return;
        practiceObject.SetActive(true);
        showToothButton.gameObject.SetActive(false);
        ApplyFadedControllerMaterial();
        GoTo(Step.RotateToView);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRBaseControllerInteractor controller)
            controller.SendHapticImpulse(0.35f, 0.08f);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (step != Step.RotateToView) return;
        if (viewTracker != null && viewTracker.CurrentView == targetView)
            GoTo(Step.OpenMenu);
    }

    private void GoTo(Step next)
    {
        step = next;

        SetHintGroup(indexTriggerHints, next == Step.ShowTooth || next == Step.ShowCusps ||
                                        next == Step.SelectCusp || next == Step.Done);
        SetHintGroup(gripHints, next == Step.RotateToView);
        SetHintGroup(joystickHints, false);
        SetHintGroup(menuButtonHints, next == Step.OpenMenu);

        switch (next)
        {
            case Step.ShowTooth:
                instructionsText.text =
                    "Welcome! Hold the controllers naturally: index finger on the front trigger, " +
                    "middle finger on the side grip, thumb on the joystick.\n\n" +
                    "Point the ray at \"Show Tooth\" and pull the index trigger to click it.";
                break;

            case Step.RotateToView:
                instructionsText.text =
                    "This is a maxillary right second molar.\n\n" +
                    "Point at the tooth, then squeeze and hold the side grip. Move the ray to turn it " +
                    "until the occlusal (biting) surface faces you, then let go.";
                break;

            case Step.OpenMenu:
                instructionsText.text =
                    "Nice, that's the occlusal view!\n\n" +
                    "Press B on the right controller, or the Menu button on the left controller, " +
                    "to open the Settings menu.";
                break;

            case Step.ShowCusps:
                instructionsText.text =
                    "This is the Settings menu.\n\n" +
                    "Under \"Show structures\", point at \"Cusps\" and click the trigger to turn them on.";
                break;

            case Step.SelectCusp:
                instructionsText.text =
                    "The cusps are now highlighted on the tooth.\n\n" +
                    "Close the menu, or drag it aside by its handle, then point at one of the cusps " +
                    "and click the trigger to read about it."+
                    "You can click the trigger again to close the information window";
                break;

            case Step.Done:
                instructionsText.text =
                    "Well done! You've finished the tutorial.\n\n" +
                    "Click \"Enter Study Mode\" to start studying.";
                enterStudyButton.gameObject.SetActive(true);
                break;
        }
    }

    private static void SetHintGroup(GameObject[] hints, bool isActive)
    {
        if (hints == null) return;
        foreach (GameObject hint in hints)
            if (hint != null) hint.SetActive(isActive);
    }

    private void ApplyFadedControllerMaterial()
    {
        if (fadedControllerMaterial == null || controllerGuideRenderers == null) return;
        foreach (Renderer r in controllerGuideRenderers)
        {
            if (r == null) continue;
            Material[] materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = fadedControllerMaterial;
            r.sharedMaterials = materials;
        }
    }

    private void EnterStudyMode() => SceneManager.LoadScene(studyScene);
}