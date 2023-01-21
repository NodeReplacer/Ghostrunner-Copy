using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Detects collision. There's an issue here in that the CharacterController.Move pushes the capsule away from the surface
//enough that there is no more collision occuring, so we exit the collision immediately on contact.

//Be certain to catch that because if we rely on staying collided we'll be disappointed.

//Make FirstPersonController stop taking input while running
//Move character controller in direction of wall until jump.

namespace StarterAssets
{
    public class WallRun : MonoBehaviour
    {
        [SerializeField] 
        private FirstPersonController _firstPersonController;
        
        [SerializeField] 
        private GameObject playerCamera;
       
        [SerializeField] 
        [Tooltip("Ensure we ignore this object for the sake of collision detection.")]
        private GameObject mySelf;
        
        [SerializeField, Range(0, 90)]
        [Tooltip(
            "The nature of this is very difficult to explain. 90 degrees is a straight wall, while 180 is a ceiling. The numbers in between " +
            "the two are overhangs. Or upside down slopes if you'd like to call it that. \n\n" +
            "minWallRunAngle must be less than 90 degrees")]
        float minWallRunAngle = 80.0f;
        
        [SerializeField, Range(90,180)]
        [Tooltip("MaxWallRunAngle must be 90 degrees or greater. No exceptions.")]
        private float maxWallRunAngle = 100.0f;
        //The fuller explanation: This angle is measured relative to the floor. One edge is our "wall" or the face we are
        //trying to figure out is a wall or not. The other edge is a floor that doesn't exist in game but we are measuring from
        //there.
        //This angle faces away from our player. So 10 degrees would be a ramp to our player. 90 is a wall. Above 90 is an overhang.
        //and 180 is a ceiling. This is what the tooltip above was referring to.
        //So minWallRunAngle being less than 90 and maxWallRunAngle being greater than 90 puts them in separate quadrants.
        //Which means that maxWallRunAngle will resolve cos as negative as long as it is greater than 90.
        //We check if our own cos is greater than maxWallRunAngle to make sure it is in our wall angle range.
        //This is because Cos(For example: 100) returns a value that is less than Cos(99).
        //You'd think Cos(100) would be bigger but it is a negative number so by being MORE negative it's lower than Cos(99).
        //That's fine so far. Just check if our surface contact's cos is less than our MaxWallRunAngle.
        //Let's write that as updot >= maxWallRunDotProduct.
        //
        //(What are these mentionings of dots? A dot product is equal to the cos(angle between two given lines). Remember in our
        //problem we are not actually being given angles just surface contacts and normals. Which are straight lines and no more.
        //We KNOW what angle we want and need to solve to find some way to check which we are doing now).
        //
        //But now let's step back into the first quardrant (degrees 0 -> 90).
        //Cos(81) = 0.15
        //Cos(80) = 0.17
        //Now the bigger cos number is cos(80). So if our maxWallRunAngle is 80 and our surface contact is 81 the equation
        //updot >= maxWallRunDotProduct will pass as true. Which is a shorthand way of saying that our equation will tell us
        //that despite our maxWallRunAngle of 80 degrees, 81 degrees fits inside it. But it shouldn't. 80 is our max.
        //
        //The thing that messes us up is that we are stepping outside of our quadrant right? So just limit us in there.
        //The alternative is to just find the angle of our surface contact but if we could do that we'd just have the angle
        //which means we wouldn't need to trouble ourselves like this.
        
        [SerializeField]
        [Tooltip("The rate at which our wallJump will slow down.")]
        private float _wallJumpFalloff = -15f;

        public float wallJumpSpeed = 0f;
        
        public Vector3 wallRunDirection;
        public Vector3 wallJumpDirection;
        
        public bool isWallRunning = false; //We start out not wall running.
        
        private CharacterController _characterController;
        private DodgeAndDash _dodgeAndDash;
        private StarterAssetsInputs _input;
        
        private bool _isColliding;
        
        private float _minWallRunDotProduct;
        private float _maxWallRunDotProduct;
        
        void Awake()
        {
            _minWallRunDotProduct = Mathf.Cos(minWallRunAngle * Mathf.Deg2Rad);
            _maxWallRunDotProduct = Mathf.Cos((maxWallRunAngle) * Mathf.Deg2Rad);
            
            _characterController = GetComponent<CharacterController>();
            _firstPersonController = GetComponent<FirstPersonController>();
            _dodgeAndDash = GetComponent<DodgeAndDash>();
            
            Physics.IgnoreCollision(mySelf.GetComponent<Collider>(), GetComponent<Collider>());
        }

        private void Update()
        {
            if (wallJumpSpeed > 0)
            {
                Debug.Log("Current wallJumpSpeed = "+wallJumpSpeed);
                wallJumpSpeed += _wallJumpFalloff * Time.deltaTime;
            }
            else
            {
                wallJumpSpeed = 0;
            }
        }
        private void FixedUpdate()
        {
            //Hopefully this makes the collision check only check once.
            _isColliding = false;
        }

        //I'll have to rewrite this.
        //On enter should simply evaluate collisions then. Because there's no way to do it
        void OnCollisionEnter(Collision collision)
        {
            if(_isColliding) return;
            _isColliding = true;
            
            _dodgeAndDash.isDashing = false;
            _dodgeAndDash.hasDashTarget = false;
            
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal.normalized;
                EvaluateCollision(collision, normal);
            }
        }

        private void StartWalLRun()
        {
            //Handle the camera turn here when the player hits a new surface that they can wallrun on.
            //The checks for wall run validity occur in EvaluateCollision.
        }
        
        private void EvaluateCollision(Collision collision, Vector3 normal)
        {
            wallRunDirection = ProjectDirectionOnPlane(playerCamera.transform.forward, normal);
            wallRunDirection.y = 0;
            Debug.DrawRay(_firstPersonController.transform.position,wallRunDirection.normalized,Color.red,15);
            
            float updot = Vector3.Dot(Vector3.up, normal);
            
            //Make an if statement here to determine if the collision counts as a wall.
            //Compare it with our maximum allowed "wall angle"
            if (updot >= _maxWallRunDotProduct && updot <= _minWallRunDotProduct)
            {
                Debug.DrawRay(_firstPersonController.transform.position,normal,Color.blue,15);
                //Ensure we are not on the ground
                if (!_firstPersonController.Grounded)
                {
                    //NOTE: For looks we can lerp the rotation. It happens in a single frame for now.
                    Quaternion rotation = Quaternion.LookRotation(wallRunDirection.normalized);
                    transform.rotation = rotation;
                    if (isWallRunning)
                    {
                        //Debug.Log("We have entered a new collision but are currently wallrunning. Need to transfer walls");
                    }
                    isWallRunning = true;
                    wallJumpDirection = normal;
                }
                else
                {
                    Debug.Log("Grounded Wall Contact");
                }
            }
        }
        
        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                //Can't I just say "if it's the same collider as before then ignore it"?
                Vector3 normal = collision.GetContact(i).normal;
                EvaluateStay(collision, normal);
            }
        }

        private void EvaluateStay(Collision collision, Vector3 normal)
        {
            float updot = Vector3.Dot(Vector3.up, normal.normalized);
            if (updot >= _maxWallRunDotProduct && updot <= _minWallRunDotProduct)
            {
                if (_firstPersonController.Grounded)
                {
                    //Debug.Log("Still grounded but staying on the wall");
                }
                else
                {
                    //Debug.Log("Staying on the wall but no longer grounded.");
                }
            }
        }
        
        void OnCollisionExit(Collision collision)
        {
            //Will need to check if our collision exit is just transferring walls.
            Debug.Log("Collision exited safely.");
            if (isWallRunning)
            {
                isWallRunning = false;
            }
        }

        public void WallJump()
        {
            //wall jump direction was established in EvaluateCollision.
            //But the signal to start a walljump is put here.
            
            //Stop wall running to allow jump movement to take over.
            isWallRunning = false;
            
            //debug - show user the direction of the jump in green.
            Debug.DrawRay(transform.position,wallJumpDirection,Color.green,15f);
            
            wallJumpSpeed = Mathf.Sqrt(_firstPersonController.JumpHeight * -2f * _firstPersonController.Gravity);

            //After all this is done. Go back to FirstPersonController and return this value to FirstPersonController's
            //own WallJumpDirection (or whatever we call it). Once done FPC's CharacterController.Move() function can use this
            //to handle the rest.
        }
        
        private Vector3 ProjectDirectionOnPlane (Vector3 direction, Vector3 normal) {
            //Arguments: Direction is the Vector3 we want to project onto the plane.
            //           Normal is the normal vector of the plane in question.
            //The normal vector of the plane in question is usually gotten by using getContact(INDEX).normal;
            return (direction - normal * Vector3.Dot(direction, normal)).normalized;
        }
        
    }
}