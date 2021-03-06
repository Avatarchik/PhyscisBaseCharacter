﻿using UnityEngine;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    // SINGLETON PATTERN
    private static InputManager instance;
    public static InputManager Instance { get { return instance; } }    // Public  static ref

    //DELEGATEs -   -   -   -   -
    // Jump Delegate
    public delegate void OnJumpDelegate();
    public event OnJumpDelegate jumpDelegate;
    // interation funtions
    public delegate void OnActionDelegate();
    public event OnActionDelegate actionDelegate;

    // State
    private bool IsEnable;   // set if is enable 
    private Rect JoyDetectionArea;   // Safe area for the joy action detection

    // action button pressed
    // evaluate if the button as been realeaded after pressed
    private bool actionPressed = false;
    private RectTransform inputCanvasRect; // reff to the input canvas
    private Rect SafeZoneFixedToCanvas = Rect.zero;  // safe soze calculated for the canvas size

    // JOYSTICK INFO    -   -   -   -   -
    private bool JoyStickVisible;   // Current joy visible state
    private float JoyMaxRadius; // Max Radius for the joy

    // Touch information
    private int ValidJoyTouchID;    // Current valid touch
    private Vector2 StartPosition;  // Start and end position of the touch
    private Vector2 InputVector;    // Vector from the position to the current pos

    // Joy visuals and behaviour
    private RawImage joyBackImg, JoyKnobImg;    // Ref to raw img for the back and knob joy
    private RectTransform joyBackRect, joyKnobRect; // Ref to rect transform for the back and knob joy
    private Vector2 RelatedDefaultPosition = Vector2.zero;  // default position to be calculated based on detection area
    private Color DefaultAlphaPercent = new Color(1f, 1f, 1f, 0.25f);  // Default value to alfa when stationary

    // Jump button reff
    private Image jumpBtnImg;   // ref to the button image
    private Button jumpBtn;     // ref to the button component
    private RectTransform jumpBtnRct;   // ref to the button rect transfrom

    // EXPOSED VARS -   -   -   -   -
    /// JoyStick    -   -   -   -   -
    [Header("Input Settings - JoyStick")]
    [Tooltip("Percet of screen from left used to joy Detection"), Range(.1f, .5f)] public float ScreenPercentDetect = 0.25f;
    [Header("JoyStick Behaviour")]
    [Tooltip("Mark If Joy should Return to position")] public bool shouldReturnToDefault = true;
    [Tooltip("Default position")] public Vector2 positionPercent = Vector2.zero;
    // JUMP BUTTON  -   -   -   -   -
    [Space(5f)]
    [Header("Input Setting - Jump Button")]
    [Tooltip("Margin from bottom and top")] public Vector2 JumpPercentMargin = Vector2.zero;

    // On Awake check the singleton values
    private void Awake()
    {
        // check if the singleton exists or not
        if (instance == null) instance = this;
        // If exists and is different from this, destroy
        else if (InputManager.Instance != this) Destroy(this);
        // Display information
        InformationPanel.DebugConsoleInput("Input Manager Singleton validation!");
    }
    private void Start()
    {
        // Initialize the singleton
        InitSingleton();
    }
    private void FixedUpdate()
    {
        // call registed delegates
        // Press Action
        if (GetActionValue()) actionDelegate();
    }
    private void Update()
    {
        // if is enable
        if (!IsEnable) return;
        // Joysitch input
        OnScreenController();
        // ACtion Resolver
    }


    #region  Public Methods
    ///<Summary>
    /// Get the input direction
    ///</Summary>
    public Vector2 GetInputVector()
    {        // Return the a vector based on vertical and horizontal input values
        return new Vector2(-InputVector.x, InputVector.y);
    }

    /// <summary>
    /// Called when pressed the jump button
    /// </summary>
    public void JumbButtonPressCall() =>
        // call the delegate event to jump input
        jumpDelegate();


    #endregion

    #region Private Methods
    ///<Summary>
    /// Init Singleton instance
    ///</Summary>
    public void InitSingleton()
    {
        // Set as enable
        this.IsEnable = true;

        // calculate the canvas proportion
        this.inputCanvasRect = this.GetComponent<RectTransform>();
        this.SafeZoneFixedToCanvas = AplicationFuncs.SafeToSafeCanvas(canvasRect: inputCanvasRect.rect);

        Debug.Log(SafeZoneFixedToCanvas);
        // calculate the area for joyDetection
        JoyDetectionArea =
            new Rect(SafeZoneFixedToCanvas.xMin, SafeZoneFixedToCanvas.yMin,
             SafeZoneFixedToCanvas.width * ScreenPercentDetect, SafeZoneFixedToCanvas.height);


        // get the joyStick REffs, knowing that has a child that is a joyBack and a child of that been the knob
        // back part of the joy
        this.joyBackImg = this.transform.GetChild(0).GetComponent<RawImage>();
        this.joyBackRect = this.joyBackImg.GetComponent<RectTransform>();

        // knoob part of the joy
        this.JoyKnobImg = this.joyBackImg.transform.GetChild(0).GetComponent<RawImage>();
        this.joyKnobRect = this.JoyKnobImg.GetComponent<RectTransform>();

        // Calculate the maximus radius for the joy based on size
        this.JoyMaxRadius = this.joyBackRect.rect.width * 0.5f - this.joyKnobRect.rect.width * 0.5f;

        // calculate the default related position, validating the values making joy visivel on safe area all the time
        this.RelatedDefaultPosition = new Vector2(
            JoyDetectionArea.position.x +
                ((JoyDetectionArea.width * positionPercent.x) < (joyBackRect.rect.width / 2f) ?
                (joyBackRect.rect.width / 2f) : (JoyDetectionArea.width * positionPercent.x)),
            JoyDetectionArea.position.y +
                ((JoyDetectionArea.height * positionPercent.y) < (joyBackRect.rect.height / 2f) ?
                (joyBackRect.rect.height / 2f) : (JoyDetectionArea.height * positionPercent.y)));

        // get refference to the jump button components
        this.jumpBtnImg = this.transform.GetChild(1).GetComponent<Image>();
        this.jumpBtn = this.jumpBtnImg.GetComponent<Button>();
        this.jumpBtnRct = this.jumpBtnImg.GetComponent<RectTransform>();

        // calculate the jump button position based on margin and placed in safe area
        this.jumpBtnRct.anchoredPosition = new Vector2(
            SafeZoneFixedToCanvas.xMax - (SafeZoneFixedToCanvas.width * JumpPercentMargin.x + jumpBtnRct.rect.width * 0.5f),
            SafeZoneFixedToCanvas.yMin + SafeZoneFixedToCanvas.height * JumpPercentMargin.y + jumpBtnRct.rect.height * 0.5f);

        // Set joy to invisible
        IsJoyStickVisible = false;

        // Debug thats ready
        InformationPanel.DebugConsoleInput("Input Manager valid and Online!");
    }

    /// <summary>
    /// Evaluates, draw and calculte the input on screen
    /// </summary>
    private void OnScreenController()
    {
        // Check if exists toutchs or the joysitck is desable
        if (Input.touchCount == 0f) return;

        // Evaluate all the touches
        foreach (var touch in Input.touches)
        {
            // check if exist a toutch beeing tracked
            if (this.IsJoyStickVisible && touch.fingerId == ValidJoyTouchID)
            {
                // if the touch ended
                if (touch.phase == TouchPhase.Ended)
                {// set the joy invisible
                    IsJoyStickVisible = false;
                    // jump to the next touch
                    break;
                }
                // if the joy is visible, a toutch is been tracked and not ended, update the 
                // calculate the delta vector
                // clamp the maximu magnitude to the maximun calculated radius
                var tempDeltaDirection = Vector2.ClampMagnitude((touch.position - this.StartPosition), this.JoyMaxRadius);

                // Set the knob positio base on the calculated delta
                joyKnobRect.anchoredPosition = (this.joyBackRect.rect.size * 0.5f) + tempDeltaDirection;

                // Determinate the amount of input the joy makes (-1 to 1 scale)
                this.InputVector.Set(tempDeltaDirection.x / this.JoyMaxRadius, tempDeltaDirection.y / this.JoyMaxRadius);
            }
            // if is inside of the joy Detection area and is a new touch
            else if (JoyDetectionArea.Contains(AplicationFuncs.ScreenToCanvasPos(touch.position, inputCanvasRect.rect)) &&
             touch.phase == TouchPhase.Began)
            {
                // set the joy to visible
                IsJoyStickVisible = true;
                // Store the information of this new valid touch
                this.ValidJoyTouchID = touch.fingerId;
                this.StartPosition = touch.position;

                // place the joy on the touch position
                this.joyBackRect.anchoredPosition = AplicationFuncs.ScreenToCanvasPos(touch.position, inputCanvasRect.rect);
            }
        }
    }


    ///<Summary>
    /// Get the action state press
    ///</Summary>
    private bool GetActionValue()
    {
        // check if the axis as a valid input
        if (Input.GetAxis("Action") == 1f && actionPressed) { actionPressed = false; return true; }
        // reset the press locker
        else if (Input.GetAxis("Action") == 0f) actionPressed = true;
        // return false if not
        return false;
    }
    #endregion

    #region Getter/Setter
    /// <summary>
    /// Get and set the state of the input controller
    /// </summary>
    /// <value>New State for the controller</value>
    public bool InputState
    {
        get { return IsEnable; }    // Return the current input State
        set
        {
            // Set a new State to the input system
            this.IsEnable = value;
            // Set the jump button to the same enable state
            this.jumpBtn.enabled = this.jumpBtnImg.enabled = this.IsEnable;

            // make sure that joy is hide
            if (!this.enabled) IsJoyStickVisible = this.enabled;
        }
    }

    /// <summary>
    /// Set and get the joystick visible state
    /// </summary>
    /// <value>Is Visible value</value>
    public bool IsJoyStickVisible
    {
        // retorn if the joystickIsVisible
        get { return this.JoyStickVisible; }
        // Set the visible state to the new value
        set
        {
            // if the imput controller is disable and a change to the visibility occurs
            if (!this.enabled)
            {
                // Set image state to the hide
                joyBackImg.enabled = JoyKnobImg.enabled = this.JoyStickVisible = false;
                // exits because the controller is disable
                return;
            }

            // Stores the new value
            this.JoyStickVisible = value;
            // Check if exists a ref to the imgs
            if (joyBackImg && JoyKnobImg)
            {
                // if the joyStick changed to hided, reset the input vector
                if (!this.JoyStickVisible)
                {
                    this.InputVector = Vector2.zero;
                    // if the joy is "hide", place the noob back to the center
                    joyKnobRect.localPosition = Vector2.zero;
                }

                //if the joy shouldnt return to default position
                if (!this.shouldReturnToDefault)
                    // Set image state to the new value
                    joyBackImg.enabled = JoyKnobImg.enabled = this.JoyStickVisible;
                // if should return to position
                else
                {
                    if (!this.JoyStickVisible)
                    {
                        // Set the alpha value of the images to the defined alpha
                        joyBackImg.color = JoyKnobImg.color = DefaultAlphaPercent;
                        // set the joy back to the default position
                        this.joyBackRect.anchoredPosition = this.RelatedDefaultPosition;
                    }
                    else
                        // Restore the color for the images
                        joyBackImg.color = JoyKnobImg.color = Color.white;
                }
            }

        }
    }
    #endregion

}