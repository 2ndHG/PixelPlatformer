using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PlayerController : Actor, IInertiaReceiver, ISolidUpdateReceiver
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

    // one-way platform jump
    private const int yOWPJumpUpExtend = 1, yOWPJumpDownExtends = 3;

    // coyote time
    private int framesAfterGround = 0;
    private const int coyoteFrames = 5; 

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
    [SerializeField] private int maxStoredInertiaFrame = 10;
    [SerializeField] private bool enableInertia;
    private Vector2 receivedInertia;
    private float xInertiaVelocity, yInertiaVelocity;
    private int storedInertiaFrame;
    [SerializeField] private float yInertiaEachPixel;
    private bool appliedInertiaThisFrame;
    #endregion

    #region Riding
    private int xAmountBeforeSquish, yAmountBeforeSquish;
    private int sideSquishCorrection = 6;
    #endregion

    #region Glove
    [SerializeField] private float gloveSpeed;
    [SerializeField] private int gloveLengthAxis = 70, gloveLengthDiagonal = 50;
    [SerializeField] private GameObject GloveHitPoint;
    private const int sizeGloveAxis = 4;
    private const int gloveStopTimeFrame = 4;
    private const int gloveTolerantFrame = 15;
    private const int gloveCorrection = 3, gloveDiagonalCorrection = 6;
    private const float yGloveEndMultipiler = 0.75f, yGloveEndUpMultipiler = 0.8f;
    private const float gloveBackToTrialPixel = 0.75f;

    private Position positionBeginGlove, positionGoalGlove, gloveExactHitPixel;
    private int gloveInputBuffer;
    private bool gloveHolding;
    private Direction8 gloveDecidedDirection;
    private int xGloveDirection, yGloveDirection;
    private bool gloveDashing;
    private bool goingToBreakGlove;
    [SerializeField] private bool puaseEditorOnGlove;
    #endregion

    #region Glove Hang
    private const int maxPixelsHangMove = 10;
    private const int hangStoreInertiaMaxFrame = 8;
    private bool gloveHanging;
    [SerializeField] private float hangMoveSpeed;
    private bool upwardHolding, downwardHolding;
    private int upInputBuffer, downInputBuffer, verticleFacing;
    private Solid hangingSolid;
    private int frameAfterHang;
    private Vector2 storedGloveInertia;
    private Position hangStartPosition, hangingSolidPoint;
    #endregion
    [SerializeField] private GameObject playerSprite, AssistanceLine;

    public PlayerController()
    {
        // 在這裡設定你想要的預設值
        UpdatePriority = (int)UpdatePriorityEnum.playerMovement;
        // Order 可以在 Unity 的 GUI 中手動設定，所以我們不在這裡設定它
    }
    enum State
    {
        Idle, Walk, Jump, Glove, GloveHang
    }

    private State currentState;
    void ChangeState(State toState)
    {
        if(toState != currentState)
        {
            currentState = toState;
        }
    }

    void CheckSolidOnFrameStart()
    {
        solidCheck.above = CheckPlatformAbove();
        solidCheck.below = CheckPlatformBelow();
        solidCheck.left = CheckPlatformLeft();
        solidCheck.right = CheckPlatformRight();
    }

    public override void PhysicUpdate()
    {
        //Debug.Log("player");
        //CheckSolidOnFrameStart();
        HandleJump();
        HandleForward();
        HandleGlove();
        HandleUpAndDown();
        CalculateVelocity();
        Move();

        
        UpdateRidingSolid();
    }

    #region Normal
    // Normal: Idle, Jump, Walk
    private void HoleCorrectionLeft() 
    {
        if (CheckPlatformLeft() && yVelocity != 0)
        {
            int sign = MathF.Sign(yVelocity);
            Vector2 assumingPosition = new(position.x - 1, position.y);
            Vector2 nearbyPosition = assumingPosition;
            nearbyPosition.y += sign;
            for (int i = 1; i <= holeCorrection; i++)
            {
                assumingPosition.y += sign;
                nearbyPosition.y += sign;
                if (!CheckPlatformLeft(assumingPosition) && CheckPlatformLeft(nearbyPosition))
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
        if (CheckPlatformRight() && yVelocity != 0)
        {
            int sign = MathF.Sign(yVelocity);
            Vector2 assumingPosition = new(position.x + 1, position.y);
            Vector2 nearbyPosition = assumingPosition;
            nearbyPosition.y += sign;
            for (int i = 1; i <= holeCorrection; i++)
            {
                assumingPosition.y += sign;
                nearbyPosition.y += sign;
                if (!CheckPlatformRight(assumingPosition) && CheckPlatformRight(nearbyPosition))
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
                    if(!CheckPlatformLeft())
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
                    if(!CheckPlatformRight())
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
        if (CheckPlatformBelow())
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
            else
            {
                if (yVelocity > 0 && (yVelocity < yJumpStartFastDecrease || !jumpHolding))
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate * yFastDecreaseMultiplier;
                else if (yVelocity < yFastFallStart && yVelocity > yFastFallEnd)
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate * yFastFallMultiplier;
                else
                    yVelocity -= GamePhysics.Gravity / GamePhysics.FrameRate;
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
            if ((leftForwardHolding && CheckPlatformLeft()) || (rightForwardHolding && CheckPlatformRight()))
                yVelocity = yVelocity < -yMaxSlideSpeed ? -yMaxSlideSpeed : yVelocity;
            else
                yVelocity = yVelocity < -yMaxGravity ? -yMaxGravity : yVelocity;

            framesAfterGround++;
        }
        if (CheckPlatformAbove())
        {
            //Hit Ceiling
            //Corner correction
            bool cornerCorrected = false;
            if (yVelocity > 0)
            {
                if (xVelocity >= 0)
                    for (int i = 1; i <= cornerCorrection; i++)
                    {
                        if (!CheckPlatformAbove(new Vector2(position.x + i, position.y)))
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
                        if (!CheckPlatformAbove(new Vector2(position.x - i, position.y)))
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
                else if (CheckPlatformBelow())
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
        if(appliedInertiaThisFrame)
        {
            appliedInertiaThisFrame = false;
            return;
        }
        if (xInertiaVelocity == 0)
            return;

        // if not forwarding the same direction of Inertia, it decrease
        if ((xInertiaVelocity < 0 && !leftForwardHolding ) || (xInertiaVelocity > 0 && !rightForwardHolding) || CheckPlatformBelow())
        {
            int sign = MathF.Sign(xInertiaVelocity);
            xInertiaVelocity -= sign * xStopAcceleration / GamePhysics.FrameRate;
            if (MathF.Sign(xInertiaVelocity) != sign)
            {
                xInertiaVelocity = 0;
            }
        } 
        else if((xInertiaVelocity < 0 && CheckPlatformLeft()) || (xInertiaVelocity > 0 && CheckPlatformRight()))
        {
            xInertiaVelocity = 0;
        }
    }
    public void NormalJump()
    {
        // cannot jump
        if (currentState == State.Glove)
            return;
        // normal jump
        ChangeState(State.Jump);

        //Inertia
        ConsumeInertiaVelocity(true);
        yInertiaVelocity = CalculateYInertiaPixel(yInertiaVelocity);

        yVelocity = yJumpVelocity + yInertiaVelocity + GamePhysics.Gravity / GamePhysics.FrameRate;
        //Debug.Log(yVelocity);
        yInertiaVelocity = 0;
        jumpInputBuffer = 0;
        frameAfterJump = 0;
        framesAfterGround = coyoteFrames + 1;
        forceJumpTimer = 0;
    }

    public void ForceJump(float yInitial, float jumpTime = 1)
    {
        //cancel wall jump
        wallJumping = false;
        forceForward = false;
        //cancel glove dashing
        BreakGlove();

        // yVelocity will be yInitial at First Frame 
        yVelocity = yInitial + GamePhysics.Gravity / GamePhysics.FrameRate;
        forceJumpTimer = jumpTime;
        ChangeState(State.Jump);

        // Cancel the Jump Input of this frame 
        jumpInputCancel = true;
    }

    public void WallJump(int direction)
    {
        // cannot jump
        if (currentState == State.Glove)
            return;

        // cancel any force jump, get back y control
        forceJumpTimer = 0;

        facing = direction;
        ChangeState(State.Jump);

        //Inertia
        //Debug.Log(receivedInertia.x+ " " + facing);
        ConsumeInertiaVelocity(true);
        yInertiaVelocity = CalculateYInertiaPixel(yInertiaVelocity);

        yVelocity = yJumpVelocity + yInertiaVelocity + GamePhysics.Gravity / GamePhysics.FrameRate;
        jumpInputBuffer = 0;
        frameAfterJump = 0;
        xVelocity = direction * xMaxSpeed;
        if(direction != Math.Sign(xInertiaVelocity))
        {
            xInertiaVelocity = 0;
        }
        wallJumping = true;
        forceForward = true;
    }
    public void LeavePlatform()
    {
        MoveY(-1);
        ConsumeInertiaVelocity();
        if (facing == MathF.Sign(xInertiaVelocity))
        {
            xVelocity += xInertiaVelocity;
            if (Math.Abs(xVelocity) > xMaxSpeed)
            {
                xInertiaVelocity = xVelocity - xMaxSpeed * facing;
                xVelocity = facing * xMaxSpeed;
            }
            //Debug.Break();
        }
        else
            xInertiaVelocity = 0;
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

        //if fall and contact a fast moving platform, reduce current inertia
        //int sign = Math.Sign(velocity.x);
        //if (sign == Math.Sign(xInertiaVelocity))
        //{
        //    xInertiaVelocity -= velocity.x;
        //    if (Math.Sign(xInertiaVelocity) != sign)
        //        xInertiaVelocity = 0;
        //}

        receivedInertia = velocity;
        storedInertiaFrame = 0;
    }
    public void ConsumeInertiaVelocity(bool willCleanStored = false)
    {
        if (storedInertiaFrame > maxStoredInertiaFrame)
            return;
        appliedInertiaThisFrame = true;
        xInertiaVelocity = facing == MathF.Sign(receivedInertia.x) ? receivedInertia.x : 0;
        //add on original inertia, will be faster;
        //xInertiaVelocity += facing == MathF.Sign(receivedInertia.x) ? receivedInertia.x : 0;

        if(willCleanStored)
        {
            yInertiaVelocity = receivedInertia.y;
            receivedInertia = Vector2.zero;
            storedInertiaFrame = maxStoredInertiaFrame;
        }
    }
    #endregion

    #region Glove
    private void UseGlove(Direction8 direction)
    {
        // disable coyote jump
        framesAfterGround = 11;

        Vector2 axisSpeed = GetDirectionSpeed(direction, gloveSpeed);

        xGloveDirection = MathF.Sign(axisSpeed.x);
        yGloveDirection = MathF.Sign(axisSpeed.y);
        float xSign = xGloveDirection;
        float ySign = yGloveDirection;
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

        float xWillMove = 0, yWillMove = 0; 
        bool contactedSolid = false;
        Solid solidToContact = null;
        if (isDiagonal)
        {
            startPoint += new Vector2(xSign, ySign);
            Vector2 endPointH = startPoint, endPointV = startPoint;
            endPointH.x -= 2 * xSign;
            endPointV.y -= 2 * ySign;
            Vector2 step = new(xSign, ySign);
            List<Solid> contactSolids = new();
            //yWillMove -= 2 * ySign;
            //xWillMove -= 2 * xSign;
            //Debug.Log(startPoint + "AND" + endPointH + "AND" + endPointV);
            for (int i=0; i<=gloveLengthDiagonal; i++)
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
                yWillMove += ySign;
                xWillMove += xSign;
            }
            foreach(Solid solid in contactSolids)
            {
                GloveHitPoint.transform.position = startPoint + new Vector2(0.5f, 0.5f);
                GloveHitPoint.transform.parent = solid.transform;
                if (solidToContact == null)
                    solidToContact = solid;
                else if (solidToContact.RidingPriority < solid.RidingPriority)
                    solidToContact = solid;
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
                startPoint.y -= sizeGloveAxis / 2;
                startPoint.x += (size.width / 2) * xSign; // reach the edge of the actor
                Vector2 endPoint = startPoint + new Vector2(0, sizeGloveAxis-1);
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
                    xWillMove += xSign;
                }
                
                foreach (Solid solid in contactSolids)
                {
                    if(solidToContact == null)
                        solidToContact = solid;
                    else if (solidToContact.RidingPriority < solid.RidingPriority)
                        solidToContact = solid;
                }
            }
            //up and down
            else
            {
                //up middle or down middle
                if (ySign == -1)
                    startPoint.y--;
                startPoint.y += size.height / 2 * ySign; // reach the edge of the actor
                startPoint.x -= sizeGloveAxis / 2;

                Vector2 endPoint = startPoint + new Vector2(sizeGloveAxis-1, 0);
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
                    yWillMove += ySign;
                }
                foreach (Solid solid in contactSolids)
                {
                    if (solidToContact == null)
                        solidToContact = solid;
                    else if (solidToContact.RidingPriority < solid.RidingPriority)
                        solidToContact = solid;
                }
            }
        }

        if(contactedSolid)
        {
            SetFacing(xGloveDirection);
            gloveDashing = true;
            gloveInputBuffer = 0;
            xRemainder = 0;
            yRemainder = 0;
            ChangeState(State.Glove);
            xVelocity = axisSpeed.x;
            yVelocity = axisSpeed.y;
            positionBeginGlove = position;
            positionGoalGlove = position;
            positionGoalGlove.x += (int)xWillMove;
            positionGoalGlove.y += (int)yWillMove;
            goingToBreakGlove = false;

            //gloveExactHitPixel = DetectGloveExactPixel(startPoint, solidToContact);
            //startPoint = new Vector2 (gloveExactHitPixel.x, gloveExactHitPixel.y);
            GloveHitPoint.transform.position = startPoint + new Vector2(.5f, .5f);
            GloveHitPoint.transform.parent = solidToContact.transform;

            //DetectGloveSurface(new Position(startPoint), solidToContact);
            UpdateRidingSolidAndTell(solidToContact);
            if(puaseEditorOnGlove)
                Debug.Break();
        }
    }
    
    private bool GloveLastStep()
    {
        int xStep = (int) MathF.Round(position.x + xRemainder + xVelocity / GamePhysics.FrameRate);
        int yStep = (int) MathF.Round(position.y + yRemainder + yVelocity / GamePhysics.FrameRate);
        // get the x at outsided edge of the player, when player is at goal
        bool xReachedGoal = false;
        bool yReachedGoal = false;
        if (IsDiagonal(gloveDecidedDirection))
        {
            //this is a very complicate detection, if x reach the goal , then check left check or right check whether the verticle pixels that close to the player overlapped with our glove-grabbed solid
            float xEdge = xGloveDirection < 0 ? xStep - 1 : xStep + size.width;
            //Debug.Log(xStep + " " + positionGoalGlove.x);
            xReachedGoal = ((xGloveDirection > 0 && xStep >= positionGoalGlove.x) 
                || (xGloveDirection < 0 && xStep <= positionGoalGlove.x)) 
                && GamePhysics.CheckSpecificSolidInArea(new Vector2(xEdge, yStep - 1), new Vector2(xEdge, yStep + size.height), GetRidingSolid());

            float yEdge = yGloveDirection < 0 ? yStep - 1 : yStep + size.height;
            yReachedGoal = ((yGloveDirection > 0 && yStep >= positionGoalGlove.y)
                || (yGloveDirection < 0 && yStep <= positionGoalGlove.y))
                && GamePhysics.CheckSpecificSolidInArea(new Vector2(xStep - 1, yEdge), new Vector2(xStep + size.width, yEdge), GetRidingSolid());
        }
        else
        {
            if (IsUpOrDown(gloveDecidedDirection))
                yReachedGoal = ((yGloveDirection > 0 && yStep > positionGoalGlove.y) || (yGloveDirection < 0 && yStep < positionGoalGlove.y));
            else if (IsLeftOrRight(gloveDecidedDirection))
                xReachedGoal = (xGloveDirection > 0 && xStep > positionGoalGlove.x) || (xGloveDirection < 0 && xStep < positionGoalGlove.x);
        }

        if (xReachedGoal || yReachedGoal)
        {
            xRemainder = 0;
            yRemainder = 0;
            int preventDeathLoop = 0;
            bool going = true;
            //Debug.Log((xReachedGoal ? "xReach" : "") + " " + (yReachedGoal ? "yReach" : "") + " Last Step");
            
            while (going)
            {
                if(preventDeathLoop > 20)
                {
                    Debug.Log("Death Loop");
                    Debug.Break();
                    break;
                }
                int yPosition = position.y, xPosition = position.x;
                if(yGloveDirection != 0)
                {
                    GloveMoveY(yGloveDirection);
                    // if player should moved, and then moved, going = true;
                    going = yPosition != position.y;
                    going = going && !(yGloveDirection > 0 ? position.y >= positionGoalGlove.y : position.y <= positionGoalGlove.y);
                }
                if(xGloveDirection != 0)
                {
                    GloveMoveX(xGloveDirection);
                    going = going && xPosition != position.x;
                    going = going && !(xGloveDirection > 0 ? position.x >= positionGoalGlove.x : position.x <= positionGoalGlove.x);
                }
                preventDeathLoop++;
            }
            Solid toHang = ridingSolid;
            //BreakGlove();
            BreakGloveToHang();
            GloveHangStart(position, toHang);
            GloveHitPoint.transform.parent = transform;
            return true;
        }
        return false;
    }
    private void CalculateXYGlove()
    {
        if (GloveLastStep())
            return;

        if (goingToBreakGlove || CheckGloveAccidentFlyOver())
        {
            BreakGlove();
            return;
        }

        bool yCorrected = false;
        bool xCorrected = false;
        bool breakingGlove = false;

        int xLeftLimit = 0, xRightLimit = 0, yDownLimit = 0, yUpLimit = 0;
        // diagonal correction
        if (xGloveDirection != 0 && yGloveDirection != 0)
        {
            int xDistance = Math.Abs(position.x - positionGoalGlove.x);
            int yDistance = Math.Abs(position.y - positionGoalGlove.y);
            
            switch(gloveDecidedDirection)
            {
                case Direction8.LeftUp:
                    xLeftLimit = gloveDiagonalCorrection - (xDistance - yDistance);
                    xRightLimit = 0;
                    yUpLimit = gloveDiagonalCorrection - (yDistance - xDistance);
                    yDownLimit = 0;
                    break;
                case Direction8.LeftDown:
                    xLeftLimit = gloveDiagonalCorrection - (xDistance - yDistance);
                    xRightLimit = 0;
                    yUpLimit = 0;
                    yDownLimit = gloveDiagonalCorrection - (yDistance - xDistance);
                    break;
                case Direction8.RightUp:
                    xLeftLimit = 0;
                    xRightLimit = gloveDiagonalCorrection - (xDistance - yDistance);
                    yUpLimit = gloveDiagonalCorrection - (yDistance - xDistance);
                    yDownLimit = 0;
                    break;
                case Direction8.RightDown:
                    xLeftLimit = 0;
                    xRightLimit = gloveDiagonalCorrection - (xDistance - yDistance);
                    yUpLimit = 0;
                    yDownLimit = gloveDiagonalCorrection - (yDistance - xDistance);
                    break;
            }
        }
        // axis align correction
        else
        {
            xLeftLimit = gloveCorrection + (position.x - positionGoalGlove.x);
            xRightLimit = gloveCorrection - (position.x - positionGoalGlove.x);
            yUpLimit = gloveCorrection - (position.y - positionGoalGlove.y);
            yDownLimit = gloveCorrection + (position.y - positionGoalGlove.y);
            //check axis align break
            if (yGloveDirection != 0 && Math.Abs(position.x - positionGoalGlove.x) > gloveCorrection)
            {
                breakingGlove = true;
                Debug.Log("Break!");
            }
            else if (xGloveDirection != 0 && Math.Abs(position.y - positionGoalGlove.y) > gloveCorrection)
            {
                breakingGlove = true;
                Debug.Log("Break!");
            }
        }

        // start correction
        if(!breakingGlove)
        {
            // verticle
            if (yGloveDirection != 0)
            {

                if (yVelocity > 0 && CheckPlatformAbove())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= xRightLimit; i++)
                    {
                        if (!CheckPlatformAbove(new Vector2(position.x + i, position.y)))
                        {
                            //Debug.Log(xRightLimit);
                            GloveMoveX(i, null);
                            yCorrected = true;
                            breakingGlove = false;
                            break;
                        }
                    }
                    if (!yCorrected)
                        for (int i = 1; i <= xLeftLimit; i++)
                        {
                            if (!CheckPlatformAbove(new Vector2(position.x - i, position.y)))
                            {
                                GloveMoveX(-i, null);
                                breakingGlove = false;
                                yCorrected = true;
                                break;
                            }
                        }
                }
                else if (yVelocity < 0 && CheckPlatformBelow())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= xRightLimit; i++)
                    {
                        if (!CheckPlatformBelow(new Vector2(position.x + i, position.y)))
                        {
                            GloveMoveX(i, null);
                            yCorrected = true;
                            breakingGlove = false;
                            break;
                        }

                    }
                    if (!yCorrected)
                        for (int i = 1; i <= xLeftLimit; i++)
                        {
                            if (!CheckPlatformBelow(new Vector2(position.x - i, position.y)))
                            {
                                GloveMoveX(-i, null);
                                breakingGlove = false;
                                yCorrected = true;
                                break;
                            }
                        }

                }
            }
            // horizontal glove
            if (xGloveDirection != 0)
            {
                if (xVelocity > 0 && CheckPlatformRight())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= yUpLimit; i++)
                    {
                        if (!CheckPlatformRight(new Vector2(position.x, position.y + i)))
                        {
                            GloveMoveY(i, null);
                            xCorrected = true;
                            breakingGlove = false;
                            break;
                        }
                    }
                    if (!xCorrected)
                    {
                        for (int i = 1; i <= yDownLimit; i++)
                            if (!CheckPlatformRight(new Vector2(position.x, position.y - i)))
                            {
                                GloveMoveY(-i, null);
                                breakingGlove = false;
                                xCorrected = true;
                                break;
                            }
                    }


                }
                else if (xVelocity < 0 && CheckPlatformLeft())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= yUpLimit; i++)
                    {
                        if (!CheckPlatformLeft(new Vector2(position.x, position.y + i)))
                        {
                            GloveMoveY(i, null);
                            xCorrected = true;
                            breakingGlove = false;
                            break;
                        }
                    }
                    if (!xCorrected)
                    {
                        for (int i = 1; i <= yDownLimit; i++)
                            if (!CheckPlatformLeft(new Vector2(position.x, position.y - i)))
                            {
                                GloveMoveY(-i, null);
                                breakingGlove = false;
                                xCorrected = true;
                                break;
                            }
                    }
                }
                //Debug.Log(breakingGlove);
            }
        }
        bool corrected = xCorrected || yCorrected;

        int xyDiff = 0;
        if (!breakingGlove && IsDiagonal(gloveDecidedDirection))
        {
            xyDiff = Math.Abs(Math.Abs(position.x - positionGoalGlove.x) - Math.Abs(position.y - positionGoalGlove.y));
            breakingGlove = xyDiff > gloveDiagonalCorrection;
        }

        if (breakingGlove)
        {
            Debug.Log("Break" + xyDiff);
            BreakGlove();
        }

        if (!breakingGlove && !corrected)
            TryBackToGloveTrial();

        framesAfterGround++;
    }
    private bool CheckGloveAccidentFlyOver()
    {
        //accident flys over the goal
        return MathF.Sign(position.x - positionGoalGlove.x) == xGloveDirection && MathF.Sign(position.y - positionGoalGlove.y) == yGloveDirection;
    }
    private void TryBackToGloveTrial()
    {
        //return;
        //Try to back to the original trial
        if (IsDiagonal(gloveDecidedDirection))
        {
            int xDistance = Math.Abs(positionGoalGlove.x - position.x);
            int yDistance = Math.Abs(positionGoalGlove.y - position.y);
            if (xDistance < yDistance)
            {
                MoveX(-gloveBackToTrialPixel * xGloveDirection);
                if(Math.Abs(positionGoalGlove.x - position.x) == Math.Abs(positionGoalGlove.y - position.y))
                    yRemainder = xRemainder = MathF.Min(xRemainder, yRemainder);
                return;
            }
            else if (xDistance > yDistance)
            {
                MoveY(-gloveBackToTrialPixel* yGloveDirection);
                if (Math.Abs(positionGoalGlove.x - position.x) == Math.Abs(positionGoalGlove.y - position.y))
                    yRemainder = xRemainder = MathF.Min(xRemainder, yRemainder);
                return;
            }
        }
    }
    private void BreakGloveByOther()
    {
        goingToBreakGlove = true;
    }
    private void BreakGloveToHang()
    {
        goingToBreakGlove = false;
        gloveDashing = false;
    }
    public void BreakGlove()
    {
        if (currentState != State.Glove)
            return;
        goingToBreakGlove = false;
        gloveDashing = false;
        forceJumpTimer = 1f;
        ChangeState(State.Jump);

        //base.UpdateRidingSolid() handles leaving from a solid to null
        base.UpdateRidingSolid();
        // update facing
        HandleForward();

        xInertiaVelocity = 0;
        //float xOuterInertia = receivedInertia.x;
        ConsumeInertiaVelocity(true);
        float xOuterInertia = xInertiaVelocity;
        if (MathF.Abs(xVelocity) > xMaxSpeed)
        {
            int sign = MathF.Sign(xVelocity);
            xInertiaVelocity = xVelocity - xMaxSpeed * sign;
            xVelocity = xMaxSpeed * sign;
        }
        xInertiaVelocity += xOuterInertia;
        //Debug.Log("Break" + xInertiaVelocity+ " " + xOuterInertia);

        yVelocity = (yVelocity + (yInertiaVelocity > 0 ? yInertiaVelocity : 0)) * yGloveEndMultipiler;
        if (gloveDecidedDirection == Direction8.Up)
            yVelocity *= yGloveEndUpMultipiler;

        if (yVelocity > yJumpVelocity)
        {
            yVelocity = yJumpVelocity + CalculateYInertiaPixel(yVelocity-yJumpVelocity);
        }
        //Debug.Log(yVelocity);
    }
    private Position DetectGloveExactPixel(Vector2 startPoint, Solid solidToContact)
    {
        if (gloveDecidedDirection > Direction8.Down)
            return new Position (startPoint);

        Vector2 detectingPixel = startPoint;
        // detect glove exact point
        if (gloveDecidedDirection <= Direction8.Right)
        {
            detectingPixel.y += sizeGloveAxis;
            for (int i = 1; i <= gloveLengthAxis; i++)
            {
                if (GamePhysics.CheckSpecificSolidInPosition(new Vector2(detectingPixel.x, detectingPixel.y - i), solidToContact))
                {
                    detectingPixel.y -= i;
                    break;
                }
            }
        }
        else if (gloveDecidedDirection <= Direction8.Down)
        {
            detectingPixel = new(startPoint.x + 1, startPoint.y);
            bool found = GamePhysics.CheckSpecificSolidInPosition(detectingPixel, solidToContact);
            if (!found)
            {
                detectingPixel.x++;
                found = GamePhysics.CheckSpecificSolidInPosition(detectingPixel, solidToContact);
            }
            if (!found)
            {
                detectingPixel.x++;
                found = GamePhysics.CheckSpecificSolidInPosition(detectingPixel, solidToContact);
            }
            if (!found)
            {
                detectingPixel.x -= 3;
            }
        }

        return new Position(detectingPixel);
    }

    #endregion

    #region Glove Hang
    private void GloveHangStart(Position startPosition, Solid toHang)
    {
        storedGloveInertia = new Vector2(xVelocity, yVelocity);
        ChangeState(State.GloveHang);
        gloveHanging = true;
        UpdateRidingSolidAndTell(toHang);
        hangingSolid = toHang;
        hangStartPosition = startPosition;
        hangingSolidPoint = GetHangingSolidPoint();
        frameAfterHang = 0;
        xRemainder = yRemainder = 0;
        
    }
    private Position GetHangingSolidPoint()
    {
        bool checkPointX = false, checkPointY = false;
        switch (gloveDecidedDirection)
        {
            case Direction8.LeftUp:
                //left
                checkPointX = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x - 1, position.y), new Vector2(position.x - 1, position.y + size.height - 1), hangingSolid);
                //up
                checkPointY = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x, position.y + size.height), new Vector2(position.x + size.width - 1, position.y + size.height), hangingSolid);
                //Debug.Log(position.x + " " + (position.y + size.height));
                //Debug.Log(position.x - size.width - 1 + " " + (position.y + size.height));
                break;
            case Direction8.LeftDown:
                //left
                checkPointX = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x - 1, position.y), new Vector2(position.x - 1, position.y + size.height - 1), hangingSolid);
                //down
                checkPointY = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x, position.y - 1), new Vector2(position.x + size.width - 1, position.y - 1), hangingSolid);
                break;
            case Direction8.RightUp:
                //right
                checkPointX = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x + size.width, position.y), new Vector2(position.x + size.width, position.y + size.height - 1), hangingSolid);
                //up
                checkPointY = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x, position.y + size.height), new Vector2(position.x + size.width - 1, position.y + size.height), hangingSolid);
                break;
            case Direction8.RightDown:
                //right
                checkPointX = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x + size.width, position.y), new Vector2(position.x + size.width, position.y + size.height - 1), hangingSolid);
                //down
                checkPointY = GamePhysics.CheckSpecificSolidInArea(new Vector2(position.x, position.y - 1), new Vector2(position.x + size.width - 1, position.y - 1), hangingSolid);
                break;
            case Direction8.Left:
                //y = midpoint - glove detecting size /2
                return DetectGloveExactPixel(new Vector2(position.x - 1, (2 * position.y + size.height + 1) / 2 - sizeGloveAxis / 2), hangingSolid);
            case Direction8.Right:
                //y = midpoint - glove detecting size /2
                return DetectGloveExactPixel(new Vector2(position.x + size.width, (2 * position.y + size.height + 1) / 2 - sizeGloveAxis / 2), hangingSolid);
            case Direction8.Up:
                return DetectGloveExactPixel(new Vector2((2 * position.x + size.width + 1) / 2 - sizeGloveAxis / 2, position.y + size.height), hangingSolid);
            case Direction8.Down:
                return DetectGloveExactPixel(new Vector2((2 * position.x + size.width + 1) / 2 - sizeGloveAxis / 2, position.y - 1), hangingSolid);
        }
        Position returning = new(position.x + size.width / 2, position.y + size.height / 2);
        if (checkPointX)
        {
            if (xGloveDirection == -1)
                returning.x = position.x - 1;
            else if (xGloveDirection == 1)
                returning.x = position.x + size.width;
        }
        if (checkPointY)
        {
            if (yGloveDirection == -1)
                returning.y = position.y - 1;
            else if (yGloveDirection == 1)
                returning.y = position.y + size.height;
        }
        if(!checkPointX && !checkPointY)
        {
            if (xGloveDirection == -1)
            {
                returning.x = position.x - 1;
                
            }
            else if (xGloveDirection == 1)
                returning.x = position.x + size.width;
            if (yGloveDirection == -1)
                returning.y = position.y - 1;
            else if (yGloveDirection == 1)
                returning.y = position.y + size.height;
        }
        //Debug.Log(checkPointX + "OK" + checkPointY);
        return returning;
    }
    
    private void CalculateYHang()
    {
        
        if (upwardHolding || downwardHolding)
        {
            //Vector2 leftBottomEdge = GetLeftBottomPoint();
            //Vector2 rightTopEdge = GetRightTopPoint();
            //leftBottomEdge -= Vector2.one;
            //rightTopEdge += Vector2.one;
            //leftBottomEdge.y += verticleFacing;
            //rightTopEdge.y += verticleFacing;
            //// to prevent player pass through a hole
            //if (verticleFacing == 1 && (position.y - hangStartPosition.y) > 0)
            //    rightTopEdge.y -= 2;
            //else if (verticleFacing == -1 && (position.y - hangStartPosition.y) < 0)
            //    leftBottomEdge.y += 2;


            //if (GamePhysics.CheckSpecificSolidInArea(leftBottomEdge, rightTopEdge, hangingSolid))
            //    yVelocity = verticleFacing * hangMoveSpeed;
            //else
            //    yVelocity = 0;
            yVelocity = verticleFacing * hangMoveSpeed;
        }
        else
            yVelocity = 0;
    }
    private void CalculateXHang()
    {
        if (leftForwardHolding || rightForwardHolding)
        {
            //Vector2 leftBottomEdge = GetLeftBottomPoint();
            //Vector2 rightTopEdge = GetRightTopPoint();
            //leftBottomEdge -= Vector2.one;
            //rightTopEdge += Vector2.one;
            //leftBottomEdge.x += facing;
            //rightTopEdge.x += facing;
            //// to prevent player pass through a hole
            //if (facing == 1 && (position.x - hangStartPosition.x) > 0)
            //    rightTopEdge.x -= 2;
            //else if (facing == -1 && (position.x - hangStartPosition.x) < 0)
            //    leftBottomEdge.x += 2;


            //if (GamePhysics.CheckSpecificSolidInArea(leftBottomEdge, rightTopEdge, hangingSolid))
            //    xVelocity = facing * hangMoveSpeed;
            //else
            //{
            //    xVelocity = 0;
            //}
            xVelocity = facing * hangMoveSpeed;
        }
        else
            xVelocity = 0;
    }
    private bool CheckReachYMaxHang()
    {
        return Math.Abs(hangStartPosition.y - (int)(position.y + yRemainder + yVelocity / GamePhysics.FrameRate)) > maxPixelsHangMove;
    }
    private bool CheckReachXMaxHang()
    {
        return Math.Abs(hangStartPosition.x - (int)(position.x + xRemainder + xVelocity / GamePhysics.FrameRate)) > maxPixelsHangMove;
    }
    private void CalculateAndMoveHang()
    {
        if (CheckBreakHang())
            return;

        CalculateYHang();
        HangMoveY(yVelocity / GamePhysics.FrameRate);
        CalculateXHang();
        HangMoveX(xVelocity / GamePhysics.FrameRate);
        frameAfterHang++;
    }
    private bool CheckBreakHang()
    {
        Vector2 leftBottomEdge = GetLeftBottomPoint();
        Vector2 rightTopEdge = GetRightTopPoint();
        leftBottomEdge -= Vector2.one;
        rightTopEdge += Vector2.one;
        if (!gloveHolding || !GamePhysics.CheckSpecificSolidInArea(leftBottomEdge, rightTopEdge, hangingSolid))
        {
            BreakHang();
            return true;
        }
        return false;
    }
    private void BreakHang()
    {
        //Debug.Log("break Hang");
        if(frameAfterHang > hangStoreInertiaMaxFrame)
        {
            xVelocity = yVelocity = 0;
            xInertiaVelocity = 0;
        }
        else
        {
            ConsumeInertiaVelocity();
            xVelocity = Math.Abs(storedGloveInertia.x) > xMaxSpeed ? Math.Sign(storedGloveInertia.x) * xMaxSpeed : storedGloveInertia.x;
            xInertiaVelocity = Math.Abs(storedGloveInertia.x) > xMaxSpeed ? storedGloveInertia.x - xVelocity : 0;

            yVelocity = (storedGloveInertia.y + (yInertiaVelocity > 0 ? yInertiaVelocity : 0)) * yGloveEndMultipiler;
            if (gloveDecidedDirection == Direction8.Up)
                yVelocity *= yGloveEndUpMultipiler;
            if (yVelocity > yJumpVelocity)
            {
                yVelocity = yJumpVelocity + CalculateYInertiaPixel(yVelocity - yJumpVelocity);
            }
            forceJumpTimer = 1f;
            //Debug.Log(yVelocity);
        }
        gloveHanging = false;
        ChangeState(State.Idle);
    }
    private void HangMoveY(float amount, Action onCollide = null)
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
                if (preventDeadLoop == 50)
                {
                    Debug.Log("Infinite Loop");
                    Debug.Break();
                    break;
                }
                // check whether stick on solid after move
                Position checkingPoint = new();
                int middlePointX = position.x + size.width / 2;
                // set checkingPoint to outer vertex
                Vector2 leftBottom, rightTop;
                if (middlePointX < hangingSolidPoint.x)
                {
                    checkingPoint.x = position.x + size.width;
                    leftBottom = GetLeftBottomPoint();
                    rightTop.y = position.y + size.height - 1;
                    rightTop.x = position.x + size.width;
                }
                else
                {
                    checkingPoint.x = position.x - 1;
                    leftBottom.x = position.x - 1;
                    leftBottom.y = position.y;
                    rightTop = GetRightTopPoint();
                }

                bool canMove = GamePhysics.CheckSpecificSolidInArea(leftBottom, rightTop, hangingSolid);
                if(!canMove)
                {
                    if(sign < 0)
                    {
                        Vector2 newLeftBottom = new(checkingPoint.x < position.x ? checkingPoint.x : position.x, position.y - 1);
                        Vector2 newRightTop = new(checkingPoint.x > position.x ? checkingPoint.x : position.x + size.width - 1, position.y - 1);
                        canMove = GamePhysics.CheckSpecificSolidInArea(newLeftBottom, newRightTop, hangingSolid);
                    }
                    else
                    {
                        Vector2 newLeftBottom = new(checkingPoint.x < position.x ? checkingPoint.x : position.x, position.y + size.height);
                        Vector2 newRightTop = new(checkingPoint.x > position.x ? checkingPoint.x : position.x + size.width - 1, position.y + size.height);
                        canMove = GamePhysics.CheckSpecificSolidInArea(newLeftBottom, newRightTop, hangingSolid);
                    }
                }

                // reach max distance
                if (Math.Abs(position.x + sign - hangStartPosition.x) > maxPixelsHangMove)
                    canMove = false;

                if (canMove)
                {
                    if (sign < 0 ? !CheckPlatformBelow() : !CheckPlatformAbove())
                    {
                        //There is no Solid immediately beside us 
                        position.y += sign;
                        move -= sign;
                    }
                    else
                    {
                        //Hit a solid!
                        yRemainder = 0;

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
                else
                {
                    // cannot move, because of not stick on original solid
                    yRemainder = 0;
                    break;
                }
               
            }
            transform.position = new Vector2(position.x, position.y);
        }
    }
    private void HangMoveX(float amount, Action onCollide = null)
    {
        xRemainder += amount;
        int preventDeadLoop = 0;
        int move = (int)Math.Round(xRemainder);
        //Debug.Log(xRemainder);

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
                // check whether stick on solid after move
                Position checkingPoint = new();
                int middlePointY = position.y + size.height / 2;
                // set checkingPoint to outer vertex
                Vector2 leftBottom, rightTop;
                if (middlePointY < hangingSolidPoint.y)
                {
                    checkingPoint.y = position.y + size.height;
                    leftBottom = GetLeftBottomPoint();
                    rightTop.y = position.y + size.height;
                    rightTop.x = position.x + size.width - 1;
                }
                else
                {
                    checkingPoint.y = position.y - 1;
                    leftBottom.x = position.x;
                    leftBottom.y = position.y - 1;
                    rightTop = GetRightTopPoint();
                }

                bool canMove = GamePhysics.CheckSpecificSolidInArea(leftBottom, rightTop, hangingSolid);

                if (!canMove)
                {
                    if (sign < 0)
                    {
                        Vector2 newLeftBottom = new(position.x - 1, checkingPoint.y < position.y ? checkingPoint.y : position.y);
                        Vector2 newRightTop = new(position.x - 1, checkingPoint.y > position.y ? checkingPoint.y : position.y + size.height - 1);
                        canMove = GamePhysics.CheckSpecificSolidInArea(newLeftBottom, newRightTop, hangingSolid);
                    }
                    else
                    {
                        Vector2 newLeftBottom = new(position.x + size.width, checkingPoint.y < position.y ? checkingPoint.y : position.y);
                        Vector2 newRightTop = new(position.x + size.width, checkingPoint.y > position.y ? checkingPoint.y : position.y + size.height - 1);
                        canMove = GamePhysics.CheckSpecificSolidInArea(newLeftBottom, newRightTop, hangingSolid);
                    }
                }

                // reach max distance
                if (Math.Abs(position.x + sign - hangStartPosition.x) > maxPixelsHangMove)
                    canMove = false;

                if (canMove)
                {
                    if (sign < 0 ? !CheckPlatformLeft() : !CheckPlatformRight())
                    {
                        //There is no Solid immediately beside us 
                        position.x += sign;
                        move -= sign;
                    }
                    else
                    {
                        //Hit a solid!
                        xRemainder = 0;
                        if (onCollide != null)
                        {
                            xAmountBeforeSquish = move;
                            yAmountBeforeSquish = 0;
                            onCollide();
                            ResetAmountBeforeSquish();
                        }
                        break;
                    }
                }
                else
                {
                    //Debug.Log(new Vector2(position.x, checkingPoint.y) + " " + new Vector2(position.x + size.width - 1, checkingPoint.y));
                    // cannot move, because of not stick on original solid
                    xRemainder = 0;
                    break;
                }
                transform.position = new Vector2(position.x, position.y);

            }
        }
    }
    #endregion
    #region General Loop
    public void CalculateVelocity()
    {
        if(currentState == State.Walk || currentState == State.Jump || currentState == State.Idle)
        {
            CalculateXNormal();
            CalculateYNormal();
            CalculateXInertia();
        }
        else if(currentState == State.Glove)
        {
            CalculateXYGlove();
        }
        else if(currentState == State.GloveHang)
        {
            CalculateAndMoveHang();
        }
    }
    private void Move()
    {
        if (currentState == State.Walk || currentState == State.Jump || currentState == State.Idle)
        {
            MoveY(yVelocity / GamePhysics.FrameRate, null);
            MoveX((xVelocity + xInertiaVelocity) / GamePhysics.FrameRate, null);

        } 
        else if(currentState == State.Glove)
        {
            GloveMoveY(yVelocity / GamePhysics.FrameRate, null);
            GloveMoveX(xVelocity / GamePhysics.FrameRate, null);
        }
        else if (currentState == State.GloveHang)
        {
            // do nothing here, move already combine into calculate.
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

        if (jumpInputBuffer > 0)
        {
            bool solidLeft = false;
            bool solidRight = false;
            Vector2 positionL = new(position.x, position.y);
            Vector2 positionR = positionL;
            for (int i=0; i<=wallJumpTolerant; i++)
            {
                if (!solidLeft)
                {
                    solidLeft = solidLeft || CheckPlatformLeft(positionL);
                    positionL.x--;
                }
                if (!solidRight)
                {
                    solidRight = solidRight || CheckPlatformRight(positionR);
                    positionR.x++;
                }
            }

            bool normalJump = CheckPlatformBelow() || framesAfterGround <= coyoteFrames;
            if(yVelocity > 0)
            {
                for (int i = -yOWPJumpDownExtends; !normalJump && i <= yOWPJumpUpExtend; i++)
                {
                    if (i != 0)
                    {
                        //the -1 is like check below
                        Solid[] platforms = GamePhysics.GetHorizontalSolids(new Vector2(position.x, position.y + i - 1), new Vector2(position.x + size.width - 1, position.y + i - 1));
                        foreach (Solid platform in platforms)
                        {
                            if (platform.IsOneWayPlatform() && platform.GetComponent<OneWayPlatform>().collidingDirection == Direction8.Up)
                            {
                                normalJump = true;
                                Debug.Log("GO");
                                break;
                            }
                        }

                    }
                }
            }

            if (normalJump)
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
        if (gloveDashing)
            return;

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
    public void HandleGlove()
    {
        if (gloveInputBuffer == gloveTolerantFrame)
            gloveDecidedDirection = GetDirection();
        if (gloveInputBuffer > 0)
        {
            UseGlove(gloveDecidedDirection);
        }
        if (gloveDashing && !gloveHolding)
        {
            goingToBreakGlove = true;
        }
        //prevent first frame cast, because the direction is not decided.
        //else if (gloveInputBuffer == gloveTolerantFrame - 1)
        //    gloveDecidedDirection = GetDirection();
        gloveInputBuffer--;
    }
    public void HandleUpAndDown()
    {
        if (upInputBuffer > 0)
        {
            upInputBuffer = 0;
            verticleFacing = 1;
        }
        else if (downInputBuffer > 0)
        {
            downInputBuffer = 0;
            verticleFacing = -1;
        }
        else if (upwardHolding && !downwardHolding)
            verticleFacing = 1;
        else if (downwardHolding && !upwardHolding)
            verticleFacing = -1;
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
        
        jumpHolding = Input.GetKey(KeyCode.Z);

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
        #region Glove
        if(Input.GetKeyDown(KeyCode.C))
        {
            GamePhysics.EngineStop(gloveStopTimeFrame);
            gloveInputBuffer = gloveTolerantFrame;
        }
        gloveHolding = Input.GetKey(KeyCode.C);
        #endregion
        #region Upward And Downward
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            upInputBuffer = 10;
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            downInputBuffer = 10;
        }
        if (Input.GetKey(KeyCode.UpArrow))
            upwardHolding = true;
        else
            upwardHolding = false;
        if (Input.GetKey(KeyCode.DownArrow))
            downwardHolding = true;
        else
            downwardHolding = false;
        #endregion
    }
    #endregion

    #region Riding
    protected override void UpdateRidingSolid()
    {
        if(currentState == State.Idle || currentState == State.Walk || currentState == State.Jump)
        {

            if (yVelocity == 0 && framesAfterGround == 0)
            {
                // leave a solid platform before next frame that its x catch us again.
                Solid ridingSolid = GetRidingSolid();
                base.UpdateRidingSolid();
                if(GetRidingSolid() != ridingSolid)
                {
                    LeavePlatform();
                }
                return;
            }
            else
            {
                Solid[] toRideSolids = null;
                Solid toRide = null;

                // wall slide
                if (leftForwardHolding && CheckPlatformLeft())
                {
                    toRideSolids = GamePhysics.GetOverlappedSolids(new Vector2(position.x - 1, position.y), new Vector2(position.x - 1, position.y + size.height));

                }
                else if (rightForwardHolding && CheckPlatformRight())
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
        else if(currentState == State.Glove)
        {
            //do nothing
        }
    
    }
    public void SolidUpdatesPosition(Solid updater, int xAmount, int yAmount)
    {
        if(GetRidingSolid() == updater)
        {
            if(gloveDashing)
            {
                positionGoalGlove.x += xAmount;
                positionGoalGlove.y += yAmount;
            }
            if (gloveHanging)
            {
                hangingSolidPoint.x += xAmount;
                hangingSolidPoint.y += yAmount;
                hangStartPosition.x += xAmount;
                hangStartPosition.y += yAmount;
            }
        }
    }
    public void SolidDestroyed(Solid solid)
    {
        if (gloveDashing && GetRidingSolid() == solid)
            BreakGlove();
    }
    #endregion
    public void SetFacing(int direction)
    {
        if (direction != -1 && direction != 1)
            return;
        facing = direction;
    }
    private void ResetAmountBeforeSquish()
    {
        xAmountBeforeSquish = 0;
        yAmountBeforeSquish = 0;
    }
    public bool CheckMoveYBreakGlove(int yAmount)
    {
        if (gloveDashing && yGloveDirection != 0 && xGloveDirection == 0)
        {
            if (MathF.Sign(yAmount) == -MathF.Sign(yGloveDirection))
            {
                Debug.Log("break by other");
                return true;
            }
        }
        return false;
    }
    public bool CheckMoveXBreakGlove(int xAmount)
    {
        if (gloveDashing && xGloveDirection != 0 && yGloveDirection == 0)
        {
            if (MathF.Sign(xAmount) == -MathF.Sign(xGloveDirection))
            {
                return true;
            }
        }
        return false;
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
                if (sign < 0 ? !CheckPlatformBelow() : !CheckPlatformAbove())
                {
                    //There is no Solid immediately beside us 
                    position.y += sign;
                    move -= sign;
                }
                else
                {
                    //Hit a solid!
                    yRemainder = 0;

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
                if (sign < 0 ? !CheckPlatformLeft() : !CheckPlatformRight())
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
    private void GloveMoveX(float amount, Action onCollide = null)
    {
        gloveDashing = false;
        MoveX(amount, onCollide);
        gloveDashing = true;
    }
    private void GloveMoveY(float amount, Action onCollide = null)
    {
        gloveDashing = false;
        MoveY(amount, onCollide);
        gloveDashing = true;
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
    private bool IsDiagonal(Direction8 direction)
    {
        return direction > Direction8.Down;
    }
    private bool IsLeftOrRight(Direction8 direction)
    {
        return direction <= Direction8.Right;
    }
    private bool IsUpOrDown(Direction8 direction)
    {
        return direction >= Direction8.Up && direction <= Direction8.Down;
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
                if (!CheckPlatformAbove(new Vector2(position.x + i, position.y)))
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
                    if (!CheckPlatformAbove(new Vector2(position.x - i, position.y)))
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
            if (!CheckPlatformAbove())
            {
                xAmountBeforeSquish = yAmountBeforeSquish = 0;
                return;
            }

            bool corrected = false;
            for (int i = 1; i <= cornerCorrection; i++)
            {
                if (!CheckPlatformAbove(new Vector2(position.x + i, position.y)))
                {
                    MoveX(i, null);
                    corrected = true;
                    break;
                }
            }
            if(!corrected)
                for (int i = 1; i <= cornerCorrection; i++)
                {
                    if (!CheckPlatformAbove(new Vector2(position.x - i, position.y)))
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
                if (sign < 0? !CheckPlatformLeft(new Vector2(position.x, position.y-i)) : !CheckPlatformRight(new Vector2(position.x, position.y - i)))
                {
                    MoveY(-i, null);
                    MoveX(xAmountBeforeSquish);
                    break;
                }
                // check the pusher solid bottom corner point
                if (sign < 0 ? !CheckPlatformRight(new Vector2(position.x - xAmountBeforeSquish, position.y - i)) : !CheckPlatformLeft(new Vector2(position.x - xAmountBeforeSquish, position.y - i)))
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

        //Debug.Log(xRemainder);
        position.x = 280;
        position.y = 140;
        ChangeState(State.Idle);
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
        playerSprite.transform.localScale = new Vector3(facing, 1, 1);
        playerSprite.transform.localPosition = new Vector3(facing == -1 ? 12 : -4, 0, 0);
        if (Input.GetKeyDown(KeyCode.L))
            AssistanceLine.SetActive(!AssistanceLine.activeSelf);
        //if (Input.GetKeyDown(KeyCode.T))
        //{
        //    Debug.Log(GamePhysics.CheckSpecificSolidInArea(new Vector2(152, 48), new Vector2(157, 48), hangingSolid));
        //}
    }
}
