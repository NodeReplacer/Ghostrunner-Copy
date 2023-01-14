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
        private GameObject mySelf;
        [Tooltip("Ensure we ignore this object for the sake of collision detection.")]
        [SerializeField, Range(0, 90)]
        float minWallRunAngle = 80.0f;
        [Tooltip(
            "The nature of this is very difficult to explain. 90 degrees is a straight wall, while 180 is a ceiling. The numbers in between" +
            "the two are overhangs. Or upside down slopes if you'd like to call it that. \n" +
            "minWallRunAngle must be less than 90 degrees")]
        [SerializeField, Range(90,180)]
        private float maxWallRunAngle = 100.0f;
        [Tooltip("MaxWallRunAngle must be 90 degrees or greater. No exceptions.")]
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
        
        public Vector3 wallRunDirection;
        
        public bool isWallRunning = false; //We start out not wall running.
        
        private CharacterController _characterController;

        private DodgeAndDash _dodgeAndDash;

        private float _minWallRunDotProduct;
        private float _maxWallRunDotProduct;
        
        void Awake()
        {
            _minWallRunDotProduct = Mathf.Cos(minWallRunAngle * Mathf.Deg2Rad);
            Debug.Log("80 * Mathf.Deg2Rad = " + (80.0f * Mathf.Deg2Rad));
            Debug.Log("In awake minWallRunDotProduct = " + _minWallRunDotProduct);
            Debug.Log("My hard code resolve = " + Mathf.Cos(80.0f * Mathf.Deg2Rad));
            _maxWallRunDotProduct = Mathf.Cos((maxWallRunAngle) * Mathf.Deg2Rad);
            _characterController = GetComponent<CharacterController>();
            _firstPersonController = GetComponent<FirstPersonController>();
            _dodgeAndDash = GetComponent<DodgeAndDash>();
            Physics.IgnoreCollision(mySelf.GetComponent<Collider>(), GetComponent<Collider>());
        }
        
        //TEMP NOTE
        //So I need to be able to transfer from two walls that are connected by a corner. An L shape.
        //Though so far it seems to work out fine because the collision counts mean this collision happens
        //after we touch our wall. There might be some issue with jumping directly at a corner but you should otherwise be fine.
        
        private void Update()
        {
            
        }
        
        void OnCollisionEnter(Collision collision)
        {
            //WARNING: The collision is still being noticed. We just aren't doing anything with
            //it unless we are not grounded.
            //This means if we touch a wall then jump while staying pressed against the wall,
            //nothing will happen even though we should probably start autorunning along
            //the side.
            Debug.Log("Entered Collision");
            if (!_firstPersonController.Grounded)
            {
                for (int i = 0; i < collision.contactCount; i++)
                {
                    //WARNING: We do not check if the contact is actually a wall or not. We could do it here
                    //using the normal and checking if the normal's y value creates an appropriate angle.
                    //There might be some ceiling running is all I'm saying.
                    Vector3 normal = collision.GetContact(i).normal;
                    EvaluateCollision(collision, normal);
                }
                /*
                foreach (ContactPoint contact in collision.contacts)
                {
                    
                }
                */
            }
        }

        private void EvaluateCollision(Collision collision, Vector3 normal)
        {
            Debug.Log("Evaluated Collision. Not Grounded. Colldied with: "+ collision.collider);
            wallRunDirection = ProjectDirectionOnPlane(playerCamera.transform.forward, normal);
            wallRunDirection.y = 0;

            //Make an if statement here to determine if the collision counts as a wall.
            //Compare it with our maximum allowed "wall angle"
            float updot = Vector3.Dot(Vector3.up, normal.normalized);
            /*
            Debug.Log("Updot = " + updot);
            Debug.Log("minWallRunDotProduct = " + _minWallRunDotProduct);
            Debug.Log("maxWallRunDotProduct = " + _maxWallRunDotProduct);
            */
            
            if (updot >= _maxWallRunDotProduct && updot <= _minWallRunDotProduct)
            {
                //What we really want to know is if the contact body's angle is between 80 -> 100 degrees.
                Debug.Log("Valid wall found");
                _dodgeAndDash.isDashing = false;
                _dodgeAndDash.hasDashTarget = false;
                
                //Tilt the camera to indicate to player
                //change headbob direction.
                //make sure the rotation is finished before we do any of this. Will need booleans
                
                //NOTE: Lerp the rotation. It happens in a single frame the way it's working now.
                //
                Quaternion rotation = Quaternion.LookRotation(wallRunDirection.normalized);
                transform.rotation = rotation;

                Debug.DrawRay(_firstPersonController.transform.position,normal,Color.blue,15);
                Debug.DrawRay(_firstPersonController.transform.position,wallRunDirection.normalized,Color.red,15);
                
                if (isWallRunning)
                {
                    Debug.Log("We have entered a new collision but are currently wallrunning. Need to transfer walls");
                }
                
                isWallRunning = true;
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
        
        private Vector3 ProjectDirectionOnPlane (Vector3 direction, Vector3 normal) {
            //Arguments: Direction is the Vector3 we want to project onto the plane.
            //           Normal is the normal vector of the plane in question.
            //The normal vector of the plane in question is usually gotten by using getContact(INDEX).normal;
            return (direction - normal * Vector3.Dot(direction, normal)).normalized;
        }

        private void RunOnWall()
        {
            //Take the speed from firstpersoncontroller
            float runSpeed = _firstPersonController.SprintSpeed;
            _characterController.Move(wallRunDirection * (_firstPersonController.MoveSpeed * Time.deltaTime));
        }
    }
}