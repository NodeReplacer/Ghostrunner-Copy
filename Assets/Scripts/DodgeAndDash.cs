using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

//Handles the logic for the dodge and parkour mechanics.
//The dodge is manual, but the input isn't handled here. it is handled in
//StarterAssetsInputs.cs with the OnDodge() function.

namespace StarterAssets
{
    public class DodgeAndDash : MonoBehaviour
    {
        [Header("Dodge Values")]
        [SerializeField] private float _dodgeTotal = 300;
        [SerializeField] private float _dodgeDrainPerSec = 100;
        [SerializeField] private float _dashDistance = 3;
        [Space]
        
        [Header("Main Camera")]
        [SerializeField] 
        private GameObject playerCamera;
        [Space]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
        private PlayerInput playerInput;
#endif
        private CharacterController controller;
        private StarterAssetsInputs input;
        private FirstPersonController characterState;

        public float dodgeEnergy; //Current dodge energy public for now just for the sake of showing the editor.
        [Header("Dash Values")]
        [SerializeField]
        public float dashSpeed = 140; //Our dash speed should divide the distance over 3 parts.
        [SerializeField] 
        private float dashDuration = 0.05f;
        [Tooltip("Duration of the dash in seconds.")]
        [Space] 
        
        public Vector3 dashTarget;
        
        public bool isDashing = false;
        public bool hasDashTarget = false;
        
        private int _dodgeCount = 1; //For now we'll say we have one dodge action available to us in midair.
        //Which means we need to have it like this.

        private Rigidbody playerRigidBody;
        
        private bool wasDodging = false;
        
        private RaycastHit aimInfo;
        
        private Vector3 aimingPoint;
        private Vector3 playerBottom; //Used for capsuleCast for aiming the player's dash
        private Vector3 playerTop; //We need the bottom and top of our capsule to be able to send it as a collidable ray.

        private bool IsCurrentDeviceMouse
        {
            get
            {
                #if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
                return playerInput.currentControlScheme == "KeyboardMouse";
                #else
				    return false;
                #endif
            }
        }
        
        private void Awake()
        {
             //What we want here is to take the firstpersoncontroller and check to see if we are grounded
             characterState = GetComponent<FirstPersonController>();
             input = GetComponent<StarterAssetsInputs>();
             controller = GetComponent<CharacterController>();
             playerRigidBody = GetComponent<Rigidbody>();
             dodgeEnergy = _dodgeTotal;
        }

        private void Start()
        {
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
            playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
            
        }
        
        //There is no need for all this "too clever for myself" stuff. A simple addforce looks good enough.

        // Update is called once per frame
        void Update()
        {
            //If our player is grounded then set the dodges he has to 1, otherwise just continue using the 
            //same dodge count. After all if it's 0 when you're in the air, an empty jump will strip the ddoge count
            //off.
            _dodgeCount = characterState.Grounded ? 1 : _dodgeCount;
            
            if (!isDashing)
            {
                DodgeMode(input.isDodging);
            }
            /*
            else
            {
                //Dash(dashTarget);
            }
            */

            //Debug Code ** Draw a line in scene view to understand how far your dash will go.
            Vector3 endPoint = transform.position+(playerCamera.transform.forward * _dashDistance);
            Vector3 startPoint = transform.position + (Vector3.up*1.375f);
            Debug.DrawLine(startPoint, endPoint,Color.red);
        }
        
        void DodgeMode(bool isActive)
        {
            //This variable "time" is used to hold our modified timescale.
            float time; 
            if (isActive && dodgeEnergy > 0 && !characterState.Grounded && _dodgeCount == 1) {
                //So while the button is held and we have dodge energy to spare.
                //AND while we're not grounded.
                //AND we haven't dodged before.
                
                //This is not a dash. It does not occupy the same space.
                
                /*
                //I'VE SAT HERE FOR HOURS THE UNITY STARTER ASSETS FIRST PERSON CONTROLLER INPUT SYSTEM IS WRETCHED
                //
                //Okay, so all I want is a signal to be sent if the key is released. GetKeyUp, right?
                //But get this, the StarterAssetsInput locks the input down into using InputValue instead of
                //any other reference to button actions.
                //
                //This is staggeringly useless because InputValue value has one, ONE, property. That is: isPressed.
                //THIS MEANS THAT THERE IS NO WAY TO FIGURE OUT IF A KEY IS BEING RELEASED FROM HERE
                //THE INPUT ASSET ACTIONS HAS A BUTTON THAT DETECTS PRESS AND RELEASE BUT YOU CAN'T USE IT BECAUSE
                //isPressed ONLY CHECKS IF IT IS PRESSED THE RELEASE SIGNAL IS JUST EATEN UP
                //
                //The direct solution is to just intercept the inputs by using GetComponent to nab the
                //InputActionReference (or just InputAction) because THAT has the pressed, cancelled, and other
                //useful properties while InputValue stands around and has one. ONE. 
                //
                //FURTHERMORE STARTERASSETSINPUT.CS IS COMPLETELY OPAQUE AS FAR AS HOW IT WORKS IT'S USELESS
                //TO TRY AND LEARN MORE IF YOU USE IT AS A REFERENCE
                //
                //YOU KNOW IF YOU JUST SET A BOOLEAN TO FALSE IT WILL ALWAYS BE FALSE MEANING ACCORDING TO THE
                //GAME THE BUTTON IS BEING RELEASED CONSTANTLY INSTEAD OF JUST ONCE WHICH IS WHAT I NEED IT TO BE.
                //
                //InputValue IS A A WRAPPER FOR InputAction.CallbackContext ANYWAYS WHY WOULD YOU MAKE A WRAPPER WITH 
                //ONE PROPERTY WHEN THE THING YOU ARE WRAPPING HAS 4 DISTINCT PROPERTIES WITH UNIQUE CONTEXTS
                //
                //It's so frustrating, if I wanted to I could easily take this thing and emulate the old 
                //Input Manager system if I needed to. I can also use Invoke Events and use InputAction.CallbackContext
                //directly. But instead I have this insane half formed shell of an input system and I have to make a call
                //on whether I want to split into two pieces as one ignores the other's existence.
                */

                //Debug.Log("Currently Dodging");
                wasDodging = true;
                time = 0.1f;
                dodgeEnergy -= _dodgeDrainPerSec * Time.unscaledDeltaTime;
                //timeElapsed += Time.unscaledDeltaTime;
            }
            else if (!isActive && wasDodging || dodgeEnergy <= 0 && wasDodging) //We are not dodging but we were dodging just a moment ago.
            {
                //Why go through this insane trouble? This is basically the button release event. But the StarterAssets
                //destroyed it. I had to then decide if I wanted two separate control schemes or to do this.
                //
                //Looking at it now. It would've been better to have a separate control scheme just mount onto the
                //existing one like it's a chassis. Lesson learned. This is a mess.

                //We were dodging but now we have released the button.
                wasDodging = false;
                time = 1.0f; //For now, we need to guarantee that time always has a timescale to work with.
                GetDashTarget();
            }
            else if (characterState.Grounded) //We are just grounded which means we head straight to dash.
            {
                dodgeEnergy = _dodgeTotal; // On landing, your dodge is reset.
                time = 1.0f;
            }
            else
            {
                //Debug.Log("Currently Not Dodging");
                time = 1.0f;
            }

            //The character dodge slows down time but the side to side movement stays stays the same speed.
            //I am unsure where to make those modifications.
            Time.timeScale = time;
        }
        
        void GetDashTarget()
        {
            bool gotHit;
            
            playerBottom = transform.position + controller.center + Vector3.up * (-controller.height * 0.25f);
            playerTop = playerBottom + Vector3.up * (controller.height * 0.55f);

            gotHit = Physics.CapsuleCast(playerBottom,playerTop,0.5f,playerCamera.transform.forward,
                out aimInfo,_dashDistance,1<<0);
            
            //Why do I do this with the dash Target? I don't need to know this I can just make it dash to the target and set
            //it appropriately.
            if (gotHit)
            {
                //We've hit something and need to stop in case we go blazing through the object.
                //May not be necessary.
                dashTarget = aimInfo.point * 0.958f;
                //There's a problem. The dash needs to activate for about 3-5 frames.
                //But this will only be called once.
                //My current solution is to just set the dash as active and deactivate once
                //we've gotten close enough to our destination.
            }
            else
            {
                //Nothing was hit so the dashTarget is directly where we are pointing.
                dashTarget = transform.position+(playerCamera.transform.forward * _dashDistance);
            }
            hasDashTarget = true;
            isDashing = true;
        }
        
        /*
        //This is an old dash method which has been moved to FirstPersonController. I felt it might be better to keep the
        //moving of the character controller constrained in one script. Though it can be taken out.
        void Dash(Vector3 targetPoint)
        {
            //Hand over the target posiiton vector3 thing to Dash this function.
            _dodgeCount = 0; //Your dodge count will come back once you land.
            
            //Dashing in the air is different from the ground.
            //In Ghostrunner, isn't it that you can touch the ground again while dodging but can still dodge where you please?
            
            //To get the direction between two points it is: Direction - Origin
            //Vector3 dashDir = (targetPoint - controller.transform.position).normalized;
            
            Debug.DrawLine(targetPoint,controller.transform.position,Color.yellow, 15f);
            
            //transform.position = Vector3.MoveTowards(transform.position, targetPoint, dashSpeed*Time.deltaTime);
            //controller.Move(dashDir * (dashSpeed * Time.deltaTime));

            if (Vector3.Distance(transform.position, targetPoint) < 0.001f)
            {
                //Now that we have gotten close enough to our target we clear our dashTarget and our "isDashing" state.
                Debug.Log("We have hit our dashTarget: "+ targetPoint);
                hasDashTarget = false;
                isDashing = false;
            }
        }
        */
        
    }
}
