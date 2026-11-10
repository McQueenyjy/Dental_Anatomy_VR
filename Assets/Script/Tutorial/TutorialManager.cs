using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

public class TutorialManager : MonoBehaviour
{
    [Header("Instruction")]
    [SerializeField] private TMP_Text instructionsText;

    [Header("Reveal Step")]
    [SerializeField] private Toggle coverToggle;
    [SerializeField] private GameObject coverObject;
    [SerializeField] private GameObject practiceCube;

    [Header("Controller Hint")]
    //[SerializeField] private GameObject controllerModel;
    [SerializeField] private GameObject[] indexTriggerHints;
    [SerializeField] private GameObject[] gripHints;
    [SerializeField] private GameObject[] joystickHints;

    [Header("Controller Guide Fade")]
    [SerializeField] private Renderer[] controllerGuideRenderers;
    [SerializeField] private Material fadedControllerMaterial;

    //[Header("Haptics")]
    //[SerializeField] private XRBaseController rightController;
    //[SerializeField] private XRBaseController leftController;

    [SerializeField] private XRGrabInteractable practiceGrab;
    [SerializeField] private Button enterStudyButton;

    private bool hasGrabbed;

    private bool hasRevealed;

    private void Awake()
    {
        coverToggle.onValueChanged.AddListener(OnCoverToggleChanged);
        practiceGrab.selectEntered.AddListener(OnCubeGrabbed);
        practiceGrab.selectExited.AddListener(OnCubeReleased);
        enterStudyButton.onClick.AddListener(EnterStudyMode);
    }
    private void Start()
    {
        hasRevealed = false;
        hasGrabbed = false;

        coverToggle.gameObject.SetActive(true);
        coverToggle.SetIsOnWithoutNotify(true);
        coverToggle.interactable = true;

        coverObject.SetActive(true);
        practiceCube.SetActive(false);

        // Your fixed demonstration controllers.
        SetHintGroup(indexTriggerHints, true);
        SetHintGroup(gripHints, true);
        SetHintGroup(joystickHints, true);

        enterStudyButton.gameObject.SetActive(false);

        instructionsText.text =
            "Hold the controllers naturally. Palms up. " +
            "Your index finger rests on the front trigger, your middle finger " +
            "rests on the side grip, and your thumb rests on the joystick.\n\n" +
            "When ready, point at \"Show protective cover\" and press the " +
            "index trigger to turn it off.";
    }

    private void OnDestroy()
    {
        if (coverToggle != null)
            coverToggle.onValueChanged.RemoveListener(OnCoverToggleChanged);
        if (practiceGrab != null)
        {
            practiceGrab.selectEntered.RemoveListener(OnCubeGrabbed);
            practiceGrab.selectExited.RemoveListener(OnCubeReleased);
        }

        if (enterStudyButton != null)
            enterStudyButton.onClick.RemoveListener(EnterStudyMode);
    }

    private void OnCoverToggleChanged(bool isOn)
    {
        // We only advance when the user turns Cover OFF.
        if (hasRevealed || isOn)
            return;

        hasRevealed = true;

        //if (rightController != null)
        //    rightController.SendHapticImpulse(0.35f, 0.08f);

        coverObject.SetActive(false);
        practiceCube.SetActive(true);

        // Leave the Toggle visible in its OFF state,
        // but prevent further changes during this tutorial step.
        //coverToggle.interactable = false;
        coverToggle.gameObject.SetActive(false);

        SetHintGroup(indexTriggerHints, false);
        SetHintGroup(gripHints, true);
        SetHintGroup(joystickHints, false);
        ApplyFadedControllerMaterial();

        instructionsText.text = 
            "Great! You revealed the practice cube.\n" +
            "Point your ray at the cube and squeeze the side grip button to select it.";

    }
    private void OnCubeGrabbed(SelectEnterEventArgs args)
    {
        if (!hasRevealed)
            return;

        hasGrabbed = true;

        if (args.interactorObject is XRBaseControllerInteractor controller)
            controller.SendHapticImpulse(0.35f, 0.08f);
        instructionsText.text =
            "Keep holding the grip and rotate your wrist to examine the cube.";
    }

    private void OnCubeReleased(SelectExitEventArgs args)
    {
        if (!hasGrabbed)
            return;

        instructionsText.text = "Great! You are ready to begin the study.";

        enterStudyButton.gameObject.SetActive(true);
        //SetHintGroup(gripHints, false);
        //SetHintGroup(joystickHints, false);
    }

    private static void SetHintGroup(GameObject[] hints, bool isActive)
    {
        if (hints == null)
            return;

        foreach (GameObject hint in hints)
        {
            if (hint != null)
                hint.SetActive(isActive);
        }
    }

    private void ApplyFadedControllerMaterial()
    {
        if (fadedControllerMaterial == null || controllerGuideRenderers == null)
            return;

        foreach (Renderer controllerRenderer in controllerGuideRenderers)
        {
            if (controllerRenderer == null)
                continue;

            Material[] materials = controllerRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = fadedControllerMaterial;

            controllerRenderer.sharedMaterials = materials;
        }
    }

    private void EnterStudyMode()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("StudyMode_1");
    }
}
