using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PlayerController : Actor, IInertiaReceiver
{
    public new int UpdatePriority = (int)UpdatePriorityEnum.playerMovement;

    [SerializeField] float yMaxGravity;
    [SerializeField] float yJumpVelocity;
    private float xRemainder, yRemainder;
    private float xVelocity, yVelocity;

    #region Frame Data
    private struct CheckSolid
    {
        public bool left, right, above, below;
    }
    CheckSolid solidCheck;
    #endregion
    #region Jump Control
    private int jumpInputBuffer, frameAfterJump;
    [SerializeField] private float yFastDecreaseMultiplier;
    [SerializeField] private int yJumpStartFastDecrease;
    [SerializeField] private float yFastFallMultiplier;
    [SerializeField] private int yFastFallStart, yFastFallEnd;
    private float forceJumpTimer;
    private bool jumpHolding;

    private readonly int cornerCorrection = 3;
    // wall jump
    private readonly int wallJumpTolerant = 2;
    private bool wallJumping;
    [SerializeField] private float yWallJumpOver;
    [SerializeField] private float wallJumpKeepYFrame;

    // coyote time
    private int framesAfterGround = 0;
    [SerializeField] private int coyoteFrames; 

    // Jump Input Cancel
    private bool jumpInputCancel;
    // slide
    [SerializeField] private float yMaxSlideSpeed;
    #endregion

    #region Forward Control
    private int facing;
    [SerializeField] private float xAcceleration, xMaxSpeed, xStopAcceleration;
    private bool leftForwardHolding, rightForwardHolding;
    private int leftInputBuffer, rightInputBuffer;
    private readonly int holeCorrection = 2;
    private bool forceForward;
    #endregion

    #region Inertia
    [SerializeField] private bool enableInertia;
    private Vector2 receivedInertia;
    private float xInertiaVelocity, yInertiaVelocity;
    [SerializeField] private int maxStoredInertiaFrame = 10;
    private int storedInertiaFrame;
    [SerializeField] private float yInertiaEachPixel;
    #endregion

    #region Riding
    private int xAmountBeforeSquish, yAmountBeforeSquish;
    private int sideSquishCorrection = 6;
    #endregion

    #region Glove
    [SerializeField] private float gloveSpeed;
    [SerializeField] private int gloveLengthAxis = 70, gloveLengthDiagonal = 50;
    [SerializeField] private GameObject GloveHitPoint;
    [SerializeField] private int gloveSizeAxis;
    #endregion
    public PlayerController()
    {
        // 在這裡設定你想要的預設值
        UpdatePriority = (int)UpdatePriorityEnum.playerMovement;
        // Order 可以在 Unity 的 GUI 中手動設定，所以我們不在這裡設定它
    }
    enum State
    {
        Idle, Walk, Jump, Glove
    }

    private State currentState;
    void ChangeState(State toState)
    {
        currentState = toState;
    }

    void CheckSolidOnFrameStart()
    {
        solidCheck.above = CheckSolidAbove();
        solidCheck.below = CheckSolidBelow();
        solidCheck.left = CheckSolidLeft();
        solidCheck.right = CheckSolidRight();
    }

    public override void PhysicUpdate()
    {
        //Debug.Log("player");
        //CheckSolidOnFrameStart();
        HandleJump();
        HandleForward();
        CalculateVelocity();
        Move();

        
        UpdateRidingSolid();
    }

    #region Normal
    // Normal: Idle, Jump, Walk

    private void HoleCorrectionLeft() 
    {
        if (CheckSolidLeft() && yVelocity != 0)
        {
            int sign = MathF.Sign(yVelocity);
            Vector2 assumingPosition = new(position.x - 1, position.y);
            Vector2 nearbyPosition = assumingPosition;
            nearbyPosition.y += sign;
            for (int i = 1; i <= holeCorrection; i++)
            {
                assumingPosition.y += sign;
                nearbyPosition.y += sign;
                if (!CheckSolidLeft(assumingPosition) && CheckSolidLeft(nearbyPosition))
                {
                    MoveY(i * sign);
                    MoveX(-1);
                    yVelocity = 0;
                }
            }
        }
    }
    private void HoleCorrectionRight()
    {
        if (CheckSolidRight() && yVelocity != 0)
        {
            int sign = MathF.Sign(yVelocity);
            Vector2 assumingPosition = new(position.x + 1, position.y);
            Vector2 nearbyPosition = assumingPosition;
            nearbyPosition.y += sign;
            for (int i = 1; i <= holeCorrection; i++)
            {
                assumingPosition.y += sign;
                nearbyPosition.y += sign;
                if (!CheckSolidRight(assumingPosition) && CheckSolidRight(nearbyPosition))
                {
                    MoveY(i * sign);
                    MoveX(1);
                    yVelocity = 0;
                }
            }
        }
    }
    private void CalculateXNormal()
    {
        //x
        // wall jump 
        if (forceForward)
        {
            facing = MathF.Sign(xVelocity);
            leftForwardHolding = rightForwardHolding = true;
        }

        if (leftForwardHolding || rightForwardHolding)
        {
            if (facing == -1 && leftForwardHolding )
            {
                if (xVelocity <= 0)
                {
                    if(!CheckSolidLeft())
                    {
                        xVelocity -= xAcceleration / GamePhysics.FrameRate;
                        xVelocity = xVelocity <= -xMaxSpeed ? -xMaxSpeed : xVelocity;
                    }
                    else
                    {
                        // if There is a solid immediately infront of us, decrease x velocity until it's zero
                        xRemainder = 0;
                        xVelocity += xStopAcceleration / GamePhysics.FrameRate;
                        if (xVelocity > 0)
                            xVelocity = 0;

                        if (forceForward)
                            forceForward = false;
                    }
                }
                else
                {
                    xVelocity -= xStopAcceleration / GamePhysics.FrameRate * 2;    // slow stop
                    //xVelocity = 0;  // sudden stop
                }

                // hole correction
                 HoleCorrectionLeft();
            }
            if (facing == 1 && rightForwardHolding)
            {
                if (xVelocity >= 0)
                {
                    if(!CheckSolidRight())
                    {
                        xVelocity += xAcceleration / GamePhysics.FrameRate;
                        xVelocity = xVelocity >= xMaxSpeed ? xMaxSpeed : xVelocity;
                    }
                    else
                    {
                        // if There is a solid immediately infront of us, decrease x velocity until it's zero
                        xVelocity -= xStopAcceleration / GamePhysics.FrameRate;
                        xRemainder = 0;
                        if (xVelocity < 0)
                            xVelocity = 0;

                        if (forceForward)
                            forceForward = false;
                    }
                }
                else
                {
                    xVelocity += xStopAcceleration / GamePhysics.FrameRate * 2;
                    //xVelocity = 0;
                }

                // hole correction
                 HoleCorrectionRight();
            }
        }
        else
        {
           
            if (Mathf.Abs(xVelocity) < xStopAcceleration / GamePhysics.FrameRate * 2)
                xVelocity = 0;
            else
                xVelocity += xVelocity > 0 ? -xStopAcceleration / GamePhysics.FrameRate : xStopAcceleration / GamePhysics.FrameRate;
        }
    } 

    private void CalculateYNormal()
    {
        // y
        // Force Jump Timer
        if (forceJumpTimer > 0)
        {
            forceJumpTimer -= 1 / GamePhysics.FrameRate;
            jumpHolding = true;
        }
        if (yVelocity <= 0)
        {
            forceJumpTimer = 0;
        }
            
        // Wall jumping
        if(wallJumping)
        {
            if (yVelocity <= yWallJumpOver)
            {
                wallJumping = false;
                forceForward = false;
            }
        }

        // gravity
        if (CheckSolidBelow())
        {
            if (currentState == State.Idle)
            {
                yRemainder = 0;
            }
            if (yVelocity <= 0)
            {
                yVelocity = 0;
                ChangeState(State.Idle);
            }
            framesAfterGround = 0;
        }
        else
        {
            if (currentState == State.Jump)
            {
                if (yVelocity > 0 && (yVelocity < yJumpStartFastDecrease || !jumpHolding) )
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate * yFastDecreaseMultiplier;
                else if (yVelocity < yFastFallStart && yVelocity > yFastFallEnd)
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate * yFastFallMultiplier;
                else
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate;
            }
            else
            {
                ChangeState(State.Jump);
                if (yVelocity < yFastFallStart && yVelocity > yFastFallEnd)
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate * yFastFallMultiplier;
                else
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate;
            }

            // slide or fall naturally
            if ((leftForwardHolding && CheckSolidLeft()) || (rightForwardHolding && CheckSolidRight()))
                yVelocity = yVelocity < -yMaxSlideSpeed ? -yMaxSlideSpeed : yVelocity;
            else
                yVelocity = yVelocity < -yMaxGravity ? -yMaxGravity : yVelocity;

            framesAfterGround++;
        }
        if (CheckSolidAbove())
        {
            //Hit Ceiling
            //Corner correction
            bool cornerCorrected = false;
            if (yVelocity > 0)
            {
                if (xVelocity >= 0)
                    for (int i = 1; i <= cornerCorrection; i++)
                    {
                        if (!CheckSolidAbove(new Vector2(position.x + i, position.y)))
                        {
                            MoveX(i, null);
                            cornerCorrected = true;
                            break;
                        }
                    }
                if (xVelocity <= 0)
                {
                    for (int i = 1; i <= cornerCorrection; i++)
                    {
                        if (!CheckSolidAbove(new Vector2(position.x - i, position.y)))
                        {
                            MoveX(-i, null);
                            cornerCorrected = true;
                            break;
                        }
                    }
                }

            }

            // Keep upward velocity on the first frames of a wall jump
            if(wallJumping && frameAfterJump <= wallJumpKeepYFrame)
            {
                yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate;
                return;
            }

            if (!cornerCorrected)
            {
                if (yVelocity > GamePhysics.Gravity / GamePhysics.FrameRate * yFastDecreaseMultiplier)
                    yVelocity = GamePhysics.Gravity / GamePhysics.FrameRate * yFastDecreaseMultiplier;
                else if (CheckSolidBelow())
                {
                    //sandwiched
                    yVelocity = 0;
                }
            }
        }
    }

    private void CalculateXInertia()
    {
        storedInertiaFrame++;

        if (xInertiaVelocity == 0)
            return;
        // if not forwarding the same direction of Inertia, it decrease
        if ((xInertiaVelocity < 0 && !leftForwardHolding ) || (xInertiaVelocity > 0 && !rightForwardHolding) || CheckSolidBelow())
        {
            int sign = MathF.Sign(xInertiaVelocity);
            xInertiaVelocity -= sign * xStopAcceleration / GamePhysics.FrameRate;
            if (MathF.Sign(xInertiaVelocity) != sign)
            {
                xInertiaVelocity = 0;
            }
        } 
        else if((xInertiaVelocity < 0 && CheckSolidLeft()) || (xInertiaVelocity > 0 && CheckSolidRight()))
        {
            xInertiaVelocity = 0;
        }
    }
    public void NormalJump()
    {
        // normal jump
        ChangeState(State.Jump);

        //Inertia
        ConsumeInertiaVelocity();
        yInertiaVelocity = CalculateYInertiaPixel(yInertiaVelocity);

        yVelocity = yJumpVelocity + yInertiaVelocity + GamePhysics.Gravity / GamePhysics.FrameRate;
        //Debug.Log(yVelocity);
        yInertiaVelocity = 0;
        jumpInputBuffer = 0;
        frameAfterJump = 0;
    }

    public void ForceJump(float yInitial, float jumpTime = 1)
    {
        //cancel wall jump
        wallJumping = false;
        forceForward = false;

        // yVelocity will be yInitial at First Frame 
        yVelocity = yInitial + GamePhysics.Gravity / GamePhysics.FrameRate;
        forceJumpTimer = jumpTime;
        ChangeState(State.Jump);

        // Cancel the Jump Input of this frame 
        jumpInputCancel = true;
    }

    public void WallJump(int direction)
    {
        // cancel any force jump, get back y control
        forceJumpTimer = 0;

        facing = -1 * facing;
        ChangeState(State.Jump);

        //Inertia
        ConsumeInertiaVelocity();
        yInertiaVelocity = CalculateYInertiaPixel(yInertiaVelocity);

        yVelocity = yJumpVelocity + yInertiaVelocity + GamePhysics.Gravity / GamePhysics.FrameRate;
        jumpInputBuffer = 0;
        frameAfterJump = 0;
        xVelocity = direction * xMaxSpeed;
        wallJumping = true;
        forceForward = true;
    }
    #endregion

    #region Inertia
    private float CalculateYInertiaPixel(float yInertia)
    {
        if (yInertia == 0)
            return 0;
        float yPixel = yInertia / yInertiaEachPixel;
        float ac = - (2 * GamePhysics.Gravity * yPixel );
        float yIncrease = (- 2 * yJumpVelocity + MathF.Sqrt(4 * yJumpVelocity * yJumpVelocity - 4 * ac) ) / 2;
        return yIncrease;
    }
    public void ReceiveVelocity(Vector2 velocity)
    {
        if (!enableInertia)
            return;

        if (velocity.y < 0)
            velocity.y = 0;
        

        receivedInertia = velocity;
        storedInertiaFrame = 0;
    }
    public void ConsumeInertiaVelocity()
    {
        if (storedInertiaFrame > maxStoredInertiaFrame)
            return;

        xInertiaVelocity = receivedInertia.x;
        yInertiaVelocity = receivedInertia.y;
        receivedInertia = Vector2.zero;
        storedInertiaFrame = maxStoredInertiaFrame;
    }
    #endregion

    #region Glove
    private void UseGlove(Direction8 direction)
    {
        Vector2 axisSpeed = GetDirectionSpeed(direction, gloveSpeed);

        float xSign = MathF.Sign(axisSpeed.x);
        float ySign = MathF.Sign(axisSpeed.y);
        Vector2 startPoint;
        bool isDiagonal = true;
        switch (direction)
        {
            case Direction8.LeftUp:
                startPoint = GetLeftTopPoint();
                break;
            case Direction8.LeftDown:
                startPoint = GetLeftBottomPoint();
                break;
            case Direction8.RightUp:
                startPoint = GetRightTopPoint();
                break;
            case Direction8.RightDown:
                startPoint = GetRightBottomPoint();
                 break;

            default:
                isDiagonal = false;
                startPoint = (GetLeftBottomPoint() + GetRightTopPoint() + Vector2.one) / 2;
                break;
        }

        startPoint -= new Vector2(xSign, ySign);

        bool contactedSolid = false;
        if (isDiagonal)
        {
            Vector2 endPointH = startPoint, endPointV = startPoint;
            endPointH.x -= 2 * xSign;
            endPointV.y -= 2 * ySign;
            Vector2 step = new(xSign, ySign);
            List<Solid> contactSolids = new();
            //Debug.Log(startPoint + "AND" + endPointH + "AND" + endPointV);
            for (int i=-2; i<gloveLengthDiagonal; i++)
            {
                Solid[] detectedSolidsH = GamePhysics.GetHorizontalSolids(startPoint, endPointH);
                foreach(Solid solid in detectedSolidsH)
                {
                    contactSolids.Add(solid);
                    contactedSolid = true;
                }
                Solid[] detectedSolidsV = GamePhysics.GetVerticleSolids(startPoint, endPointV);
                foreach (Solid solid in detectedSolidsV)
                {
                    contactSolids.Add(solid);
                    contactedSolid = true;
                }
                if (contactedSolid)
                    break;
                // next step
                
                startPoint += step;
                endPointH += step;
                endPointV += step;
                
            }
            foreach(Solid solid in contactSolids)
            {
                //Debug.Log("Grabbed: " + solid.name);
                GloveHitPoint.transform.position = startPoint + new Vector2(0.5f, 0.5f);
                GloveHitPoint.transform.parent = solid.transform;
            }
        }
        else
        {
            // left or right
            if(xSign != 0)
            {
                //left middle or right middle
                if (xSign == -1)
                    startPoint.x--;
                startPoint.y -= gloveSizeAxis / 2;
                startPoint.x += (size.width / 2) * xSign; // reach the edge of the actor

                Vector2 endPoint = startPoint + new Vector2(0, gloveSizeAxis);
                List<Solid> contactSolids = new();
                for (int i=0; i<gloveLengthAxis; i++)
                {
                    Solid[] detectedSolidsV = GamePhysics.GetVerticleSolids(startPoint, endPoint);
                    foreach (Solid solid in detectedSolidsV)
                    {
                        contactSolids.Add(solid);
                        contactedSolid = true;
                    }
                    if (contactedSolid)
                        break;
                    // next step

                    startPoint.x += xSign;
                    endPoint.x += xSign;
                }
                foreach (Solid solid in contactSolids)
                {
                    //Debug.Log("Grabbed: " + solid.name);
                    GloveHitPoint.transform.position = startPoint + new Vector2(.5f, 2.5f);
                    GloveHitPoint.transform.parent = solid.transform;
                }
            }
            //up and down
            else
            {
                //left middle or right middle
                if (ySign == -1)
                    startPoint.y--;
                startPoint.y += size.height / 2 * ySign; // reach the edge of the actor
                startPoint.x -= gloveSizeAxis / 2;

                Vector2 endPoint = startPoint + new Vector2(gloveSizeAxis, 0);
                List<Solid> contactSolids = new();
                for (int i = 0; i < gloveLengthAxis; i++)
                {
                    Solid[] detectedSolidsV = GamePhysics.GetHorizontalSolids(startPoint, endPoint);
                    foreach (Solid solid in detectedSolidsV)
                    {
                        contactSolids.Add(solid);
                        contactedSolid = true;
                    }
                    if (contactedSolid)
                        break;
                    // next step

                    startPoint.y += ySign;
                    endPoint.y += ySign;
                }
                foreach (Solid solid in contactSolids)
                {
                    //Debug.Log("Grabbed: " + solid.name);
                    GloveHitPoint.transform.position = startPoint + new Vector2(2.5f, .5f);
                    GloveHitPoint.transform.parent = solid.transform;
                }
            }
        }

        if(contactedSolid)
        {
            xRemainder = 0;
            yRemainder = 0;
            currentState = State.Glove;
            xVelocity = axisSpeed.x;
            yVelocity = axisSpeed.y;
        }
        
    }
    private void GloveBegin()
    {

    }
    #endregion
    public void CalculateVelocity()
    {
        if(currentState == State.Walk || currentState == State.Jump || currentState == State.Idle)
        {
            CalculateXNormal();
            CalculateYNormal();
            CalculateXInertia();
        }
    }
    private void Move()
    {
        if (currentState == State.Walk || currentState == State.Jump || currentState == State.Idle)
        {
            MoveY(yVelocity / GamePhysics.FrameRate, null);
            MoveX((xVelocity + xInertiaVelocity) / GamePhysics.FrameRate, null);
        } 
        else
        {
            MoveY(yVelocity / GamePhysics.FrameRate, null);
            MoveX(xVelocity / GamePhysics.FrameRate, null);
        }
    }
    public void HandleJump()
    {
        if(currentState == State.Jump)
            frameAfterJump++;
        // cancel jump this frame
        if (jumpInputCancel)
        {
            jumpInputCancel = false;
            return;
        }
        
        if (jumpInputBuffer > 0 )
        {
            bool solidLeft = false;
            bool solidRight = false;
            Vector2 positionL = new(position.x, position.y);
            Vector2 positionR = positionL;
            for (int i=0; i<=wallJumpTolerant; i++)
            {
                if (!solidLeft)
                {
                    solidLeft = solidLeft || CheckSolidLeft(positionL);
                    positionL.x--;
                }
                if (!solidRight)
                {
                    solidRight = solidRight || CheckSolidRight(positionR);
                    positionR.x++;
                }
            }

            if (CheckSolidBelow() || framesAfterGround <= coyoteFrames)
            {
                // normal jump
                NormalJump();
            } 
            else if(solidLeft && solidRight)
            {
                WallJump(-facing);
            }
            else if(solidLeft)
            {
                WallJump(1);
            }
            else if(solidRight)
            {
                WallJump(-1);
            }
            jumpInputBuffer--;
        }
    }
    public void HandleForward()
    {
        if (leftInputBuffer > 0)
        {
            leftInputBuffer = 0;
            facing = -1;
        }
        else if (rightInputBuffer > 0)
        {
            rightInputBuffer = 0;
            facing = 1;
        }
        else if (leftForwardHolding && !rightForwardHolding)
            facing = -1;
        else if (rightForwardHolding && !leftForwardHolding)
            facing = 1;
    }

    public Direction8 GetDirection()
    {
        if(Input.GetKey(KeyCode.UpArrow))
        {
            if (Input.GetKey(KeyCode.RightArrow))
                return Direction8.RightUp;
            else if (Input.GetKey(KeyCode.LeftArrow))
                return Direction8.LeftUp;
            return Direction8.Up;
        } 
        else if(Input.GetKey(KeyCode.DownArrow))
        {
            if (Input.GetKey(KeyCode.RightArrow))
                return Direction8.RightDown;
            else if (Input.GetKey(KeyCode.LeftArrow))
                return Direction8.LeftDown;
            return Direction8.Down;
        }
        if (facing == -1)
            return Direction8.Left;
        else
            return Direction8.Right;
    }
    public void HandleInput()
    {
        #region Jump Button
        if (Input.GetKeyDown(KeyCode.Z))
        {
            jumpInputBuffer = 6;
        }
        if (Input.GetKey(KeyCode.Z))
            jumpHolding = true;
        else
            jumpHolding = false;

        // Debug: Auto Jump
        //jumpInputBuffer = 5;
        //jumpHolding = true;
        #endregion
        #region Forward Button
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            leftInputBuffer = 10;
        }
        if(Input.GetKeyDown(KeyCode.RightArrow))
        {
            rightInputBuffer = 10;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
            leftForwardHolding = true;
        else
            leftForwardHolding = false;
        if (Input.GetKey(KeyCode.RightArrow))
            rightForwardHolding = true;
        else
            rightForwardHolding = false;
        #endregion
    }


    #region Riding
    protected override void UpdateRidingSolid()
    {
        if(yVelocity == 0 && framesAfterGround == 0)
        {
            base.UpdateRidingSolid();
            return;
        }
        else
        {
            Solid[] toRideSolids = null;
            Solid toRide = null;

            // wall slide
            if (leftForwardHolding && CheckSolidLeft())
            {
                toRideSolids = GamePhysics.GetOverlappedSolids(new Vector2(position.x - 1, position.y), new Vector2(position.x - 1, position.y + size.height));
                
            }
            else if (rightForwardHolding && CheckSolidRight())
            {
                toRideSolids = GamePhysics.GetOverlappedSolids(new Vector2(position.x + size.width, position.y), new Vector2(position.x + size.width, position.y + size.height));
            }

            // normal fall
            if (toRideSolids == null || toRideSolids.Length == 0)
            { 
                base.UpdateRidingSolid();
                return;
            }
            else if (toRideSolids.Length == 1)
                toRide = toRideSolids[0];
            else if (toRideSolids.Length == 2)
                toRide = toRideSolids[0].RidingPriority < toRideSolids[1].RidingPriority ? toRideSolids[0] : toRideSolids[1];

            UpdateRidingSolidAndTell(toRide);
        }
    }
    #endregion
    private void ResetAmountBeforeSquish()
    {
        xAmountBeforeSquish = 0;
        yAmountBeforeSquish = 0;
    }
    public override void MoveY(float amount, Action onCollide = null)
    {
        yRemainder += amount;
        int move = (int)Math.Round(yRemainder);
        int preventDeadLoop = 0;
        if (move != 0)
        {
            yRemainder -= move;
            int sign = Math.Sign(move);
            while (move != 0)
            {
                preventDeadLoop++;
                if(preventDeadLoop == 50)
                {
                    Debug.Log("Infinite Loop");
                    Debug.Break();
                    break;
                }
                if (sign < 0 ? !CheckSolidBelow() : !CheckSolidAbove())
                {
                    //There is no Solid immediately beside us 
                    position.y += sign;
                    move -= sign;
                }
                else
                {
                    //Hit a solid!
                    if (onCollide != null)
                    {
                        xAmountBeforeSquish = 0;
                        yAmountBeforeSquish = move;
                        onCollide();
                        ResetAmountBeforeSquish();
                    }
                    break;
                }
            }
            transform.position = new Vector2(position.x, position.y);
        }
    }
    public override void MoveX(float amount, Action onCollide = null)
    {
        xRemainder += amount;
        int preventDeadLoop = 0;
        int move = (int)Math.Round(xRemainder);
        if (move != 0)
        {
            xRemainder -= move;
            int sign = Math.Sign(move);
            while (move != 0)
            {
                preventDeadLoop++;
                if (preventDeadLoop == 50)
                {
                    Debug.Log("Infinite Loop");
                    Debug.Break();
                    break;
                }
                if (sign < 0 ? !CheckSolidLeft() : !CheckSolidRight())
                {
                    //There is no Solid immediately beside us 
                    position.x += sign;
                    move -= sign;
                }
                else
                {
                    //Hit a solid!
                    xRemainder = 0;
                    //Debug.Log((GetLeftBottomPoint() + Vector2.left)+ " " + GetRightTopPoint());
                    //Solid hitSolid = GamePhysics.GetOverlappedSolids(GetLeftBottomPoint()+Vector2.left, GetRightTopPoint())[0];
                    //Debug.Log("Hit Solid: " + hitSolid.name + " " + hitSolid.GetRightBottomPoint());
                    //Debug.Log("My Left Botton Point: " + position.x);
                    if(onCollide != null)
                    {
                        xAmountBeforeSquish = move;
                        yAmountBeforeSquish = 0;
                        onCollide();
                        ResetAmountBeforeSquish();
                    }
                    break;
                }
            }
            transform.position = new Vector2(position.x, position.y);
        }
    }
    private Vector2 GetDirectionSpeed(Direction8 direction, float speed)
    {
        Vector2 EachAxisSpeed = Vector2.zero;
        switch (direction)
        {
            case Direction8.Left:
                EachAxisSpeed.x = -1;
                break;
            case Direction8.Right:
                EachAxisSpeed.x = 1;
                break;
            case Direction8.Up:
                EachAxisSpeed.y = 1;
                break;
            case Direction8.Down:
                EachAxisSpeed.y = -1;
                break;
            case Direction8.LeftUp:
                EachAxisSpeed.x = -1;
                EachAxisSpeed.y = 1;
                break;
            case Direction8.RightUp:
                EachAxisSpeed.x = 1;
                EachAxisSpeed.y = 1;
                break;
            case Direction8.LeftDown:
                EachAxisSpeed.x = -1;
                EachAxisSpeed.y = -1;
                break;
            case Direction8.RightDown:
                EachAxisSpeed.x = 1;
                EachAxisSpeed.y = -1;
                break;
        }
        return EachAxisSpeed.normalized * speed;
    }
    private Vector2 GetDirectionLength(Direction8 direction, float Length)
    {
        Vector2 EachAxisLength = Vector2.zero;
        switch (direction)
        {
            case Direction8.Left:
                EachAxisLength.x = 1;
                break;
            case Direction8.Right:
                EachAxisLength.x = 1;
                break;
            case Direction8.Up:
                EachAxisLength.y = 1;
                break;
            case Direction8.Down:
                EachAxisLength.y = -1;
                break;
            case Direction8.LeftUp:
                EachAxisLength.x = -1;
                EachAxisLength.y = 1;
                break;
            case Direction8.RightUp:
                EachAxisLength.x = 1;
                EachAxisLength.y = 1;
                break;
            case Direction8.LeftDown:
                EachAxisLength.x = -1;
                EachAxisLength.y = -1;
                break;
            case Direction8.RightDown:
                EachAxisLength.x = 1;
                EachAxisLength.y = -1;
                break;
        }
        EachAxisLength.x = MathF.Round(EachAxisLength.x);
        EachAxisLength.y = MathF.Round(EachAxisLength.y);
        return EachAxisLength;
    }
    public override void Squish()
    {
        // ceiling corner correction
        // player is pushed upward, try leave the x axis that will hit the ceiling
        if (yAmountBeforeSquish > 0)
        {
            bool corrected = false;
            for (int i = 1; i <= cornerCorrection; i++)
            {
                if (!CheckSolidAbove(new Vector2(position.x + i, position.y)))
                {
                    MoveX(i, null);
                    MoveY(yAmountBeforeSquish);
                    corrected = true;
                    break;
                }
            }
            if (!corrected)
                for (int i = 1; i <= cornerCorrection; i++)
                {
                    if (!CheckSolidAbove(new Vector2(position.x - i, position.y)))
                    {
                        MoveX(-i, null);
                        MoveY(yAmountBeforeSquish);
                        break;
                    }
                }
        }
        // player is pushed downward, try leave the x axis that the pusher solid will come
        else if(yAmountBeforeSquish < 0)
        {
            if (!CheckSolidAbove())
            {
                xAmountBeforeSquish = yAmountBeforeSquish = 0;
                return;
            }

            bool corrected = false;
            for (int i = 1; i <= cornerCorrection; i++)
            {
                if (!CheckSolidAbove(new Vector2(position.x + i, position.y)))
                {
                    MoveX(i, null);
                    corrected = true;
                    break;
                }
            }
            if(!corrected)
                for (int i = 1; i <= cornerCorrection; i++)
                {
                    if (!CheckSolidAbove(new Vector2(position.x - i, position.y)))
                    {
                        MoveX(-i, null);
                        break;
                    }
                }
        }
        // player is pushed by side, only do downward correction
        else if( xAmountBeforeSquish != 0)
        {
            int sign = MathF.Sign(xAmountBeforeSquish);
            for (int i = 1; i <= sideSquishCorrection; i ++)
            {
                // check forward direction solid bottom corner point
                if (sign < 0? !CheckSolidLeft(new Vector2(position.x, position.y-i)) : !CheckSolidRight(new Vector2(position.x, position.y - i)))
                {
                    MoveY(-i, null);
                    MoveX(xAmountBeforeSquish);
                    break;
                }
                // check the pusher solid bottom corner point
                if (sign < 0 ? !CheckSolidRight(new Vector2(position.x - xAmountBeforeSquish, position.y - i)) : !CheckSolidLeft(new Vector2(position.x - xAmountBeforeSquish, position.y - i)))
                {
                    MoveY(-i, null);
                    MoveX(xAmountBeforeSquish);
                    break;
                }
            }
        }

        CheckOverlappingDeath();
    }
    public override void CheckOverlappingDeath()
    {
        if (GamePhysics.GetOverlappedSolids(GetLeftBottomPoint(), GetRightTopPoint()).Length == 0)
            return;

        position.x = 280;
        position.y = 135;
        transform.position = new Vector2(position.x, position.y);
    }
    private void Start()
    {
        InitializePosition();
        GamePhysics.Instance.RegisterUpdate(new PhysicsUpdateMessage(PhysicUpdate, (int)UpdatePriorityEnum.playerMovement, 0));
    }
    private void Update()
    {
        HandleInput();
        if(Input.GetKeyDown(KeyCode.C) )
        {
            UseGlove(GetDirection());
        }
    }
}
