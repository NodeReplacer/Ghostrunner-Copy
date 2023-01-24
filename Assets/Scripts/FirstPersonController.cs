using Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 2.5f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;
		
		[Header("Ghostrunner Chassis")]
		[Tooltip(
			"The variables here are not part of the ordinary Starter Assets pack. They have been added to simulate Ghostrunner's movement" +
			"style.")]
		[SerializeField] private float bobFrequency = 14f;
		[Tooltip("The head bob frequency should be a factor of speed but can be manually set here.")] 
		[SerializeField] private CinemachineImpulseSource headBobSignal;
		
		// cinemachine
		private float _cinemachineTargetPitch;
		
		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;
		private WallRun _wallRunState;
		
		private const float _threshold = 0.01f;
		
		private Transform _camOrigPos; //This is the camera's original position when attached to the user. It will
		//be modified by HeadBobOffset to perform a headbob. But if I do this won't its transform be permanently modified?
		//What will happen if I intentionally stop sprinting in the middle of a	wave then resume I'll be in a very different 
		//position.
		private float bobTime; //bobTime is used to determine how far along the sin wave we are. Our head bob
		//will be expressed as a sine wave and bobTime is just Time.deltaTime * bobFrequency
		//If we're not moving the bobTime is 0. On a sin curve 0 is our original position.
		
		private DodgeAndDash dodgeScript; //We need to receive some signals from free run to know when we are or are not dodging.
		//We have to avoid using StarterAssetsInput because we have a meter running out.
		
		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}

			dodgeScript = GetComponent<DodgeAndDash>();
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
			_wallRunState = GetComponent<WallRun>();
			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			// if there is an input
			if (_input.look.sqrMagnitude >= _threshold)
			{
				//Don't multiply mouse input by Time.deltaTime
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;
				
				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				// clamp our pitch rotation
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				// Update Cinemachine camera target pitch
				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				// rotate the player left and right
				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			//Take all of the numbers we have set up (gravity, input, jumping, etc.) and actually move
			//the controller in the direction we have discovered.
			
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
			
			if (_input.isDodging && dodgeScript.dodgeEnergy > 0 && !Grounded)
			{
				targetSpeed = targetSpeed * 5f;
			}
			
			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon
			
			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;
			
			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed =
				new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
			
			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset 
			    || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, 
					Time.fixedDeltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else if (currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, 
					Time.fixedDeltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				// move
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}
			
			//The player's movement has been settled. Here on we will be finalizing the movement and making sure we are in the correct state.

			//Bob the player's head.
			HeadBob(_speed, _input.move != Vector2.zero);

			if (_input.isDodging && dodgeScript.dodgeEnergy > 0 && !Grounded)
			{
				/*
				//There are a bunch of conditions to the dodge. I mean these problems only really arose because
				//we decided to disconnect the FreeRun code from the FirstPersonController for the sake of 
				//demonstrating what the unique part is, but it's clear that is not happening. I've been modifying this
				//code like a madman.
				
				//The central problem is that grounded dash has a cooldown.
				//Furthermore it dashes in the direction that the the player is moving.
				//So InputDirection will need to be referenced
				
				//We will not move forward, but we can move to side to side.
				
				//Yes that means you can turn and move to the side in the direction you REALLY want to go.
				//And then turn back and dash in the direction you wanted.
				
				//Holding right click does not queue up dodges. There has to be a fresh tap for it to work.
				//Then the release.
				
				//It cannot be used multiple times in the air. 
				*/
				
				//For the dodge itself:
				//Once the button is pressed it is as if the player is frozen in the air. They can move as they like after that
				//but cannot go up or down. Only along the x-z plane.
				_wallRunState.wallJumpSpeed = 0.0f;
				_verticalVelocity = 0.0f;
				_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime));
			}
			else if (dodgeScript.isDashing) //While we're dashing our normal movement cannot overwrite the motion of our dash.
			{
				transform.position = Vector3.MoveTowards(transform.position, dodgeScript.dashTarget, dodgeScript.dashSpeed*Time.deltaTime);
				if (Vector3.Distance(transform.position, dodgeScript.dashTarget) < 0.001f)
				{
					dodgeScript.isDashing = false;
				}
				_verticalVelocity = 0.0f; //Prevent gravity from building up after the end of the dash.
			}
			else if (_wallRunState.isWallRunning)
			{
				_verticalVelocity = 0.0f; //Prevent gravity from dragging the player down while they are running.
				_controller.Move(_wallRunState.wallRunDirection.normalized * (MoveSpeed * Time.deltaTime));
			}
			else
			{
				_speed = Mathf.Clamp(_speed + _wallRunState.wallJumpSpeed,0,SprintSpeed);
				_controller.Move((inputDirection.normalized + _wallRunState.wallJumpDirection) * (_speed * Time.deltaTime) + 
				                 new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime );
			}
		}
		private void HeadBob(float speed, bool isSprinting)
		{
			
			//Ensure that the player is holding sprint but is also moving and not standing still.
			if (isSprinting && speed != 0 && Grounded)
			{
				bobTime += Time.deltaTime * bobFrequency;
			}
			else
			{
				
			}
			
			if (bobTime > 1f) //It'd probably be neater to move this along a sin curve.
			{
				bobTime = 0.0f;
				headBobSignal.GenerateImpulse(Vector3.down*0.7f);
			}
		}
		
		private void JumpAndGravity()
		{
			if (Grounded || _wallRunState.isWallRunning)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					if (!_wallRunState.isWallRunning)
					{
						// the square root of H * -2 * G = how much velocity needed to reach desired height
						_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
					}
					else
					{
						//If we jump while wallrunning.
						_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
						//Debug.Log("Vertical Veloctiy = "+ _verticalVelocity);
						_wallRunState.WallJump();
					}
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
				
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}
			
			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}
		
		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}
	}
}