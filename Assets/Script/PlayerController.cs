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
    private const float yGloveEndMultipiler = 0.7f;
    private const float gloveBackToTrialPixel = 0.75f;

    private Position positionBeginGlove, positionGoalGlove;
    private int gloveInputBuffer;
    private Direction8 gloveDecidedDirection;
    private int xGloveDirection, yGloveDirection;
    private bool gloveDashing;
    private bool goingToBreakGlove;
    [SerializeField] private bool puaseEditorOnGlove;
    #endregion
    [SerializeField] private GameObject playerSprite;

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
        HandleGlove();
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
        if(appliedInertiaThisFrame)
        {
            appliedInertiaThisFrame = false;
            return;
        }
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
        //xInertiaVelocity =receivedInertia.x ;
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
            startPoint -= new Vector2(xSign, ySign);
            Vector2 endPointH = startPoint, endPointV = startPoint;
            endPointH.x -= 2 * xSign;
            endPointV.y -= 2 * ySign;
            Vector2 step = new(xSign, ySign);
            List<Solid> contactSolids = new();
            yWillMove -= 2 * ySign;
            xWillMove -= 2 * xSign;
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

            //startPoint = DetectGloveExactPixel(startPoint, solidToContact);
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
                && GamePhysics.CheckSolidInArea(new Vector2(xEdge, yStep - 1), new Vector2(xEdge, yStep + size.height), GetRidingSolid());

            float yEdge = yGloveDirection < 0 ? yStep - 1 : yStep + size.height;
            yReachedGoal = ((yGloveDirection > 0 && yStep >= positionGoalGlove.y)
                || (yGloveDirection < 0 && yStep <= positionGoalGlove.y))
                && GamePhysics.CheckSolidInArea(new Vector2(xStep - 1, yEdge), new Vector2(xStep + size.width, yEdge), GetRidingSolid());
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
            BreakGlove();
            GloveHitPoint.transform.parent = transform;
            return true;
        }
        return false;
    }
    private void CalculateXYGlove()
    {
        if (GloveLastStep())
            return;
        if(goingToBreakGlove)
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
            int xDistance = Math.Abs(position.x - positionBeginGlove.x);
            int yDistance = Math.Abs(position.y - positionBeginGlove.y);
            
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

                if (yVelocity > 0 && CheckSolidAbove())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= xRightLimit; i++)
                    {
                        if (!CheckSolidAbove(new Vector2(position.x + i, position.y)))
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
                            if (!CheckSolidAbove(new Vector2(position.x - i, position.y)))
                            {
                                GloveMoveX(-i, null);
                                breakingGlove = false;
                                yCorrected = true;
                                break;
                            }
                        }
                }
                else if (yVelocity < 0 && CheckSolidBelow())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= xRightLimit; i++)
                    {
                        if (!CheckSolidBelow(new Vector2(position.x + i, position.y)))
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
                            if (!CheckSolidBelow(new Vector2(position.x - i, position.y)))
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
                if (xVelocity > 0 && CheckSolidRight())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= yUpLimit; i++)
                    {
                        if (!CheckSolidRight(new Vector2(position.x, position.y + i)))
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
                            if (!CheckSolidRight(new Vector2(position.x, position.y - i)))
                            {
                                GloveMoveY(-i, null);
                                breakingGlove = false;
                                xCorrected = true;
                                break;
                            }
                    }


                }
                else if (xVelocity < 0 && CheckSolidLeft())
                {
                    breakingGlove = true;
                    for (int i = 1; i <= yUpLimit; i++)
                    {
                        if (!CheckSolidLeft(new Vector2(position.x, position.y + i)))
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
                            if (!CheckSolidLeft(new Vector2(position.x, position.y - i)))
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

        ConsumeInertiaVelocity();
        float xOuterInertia = xInertiaVelocity;
        if (MathF.Abs(xVelocity) > xMaxSpeed)
        {
            int sign = MathF.Sign(xVelocity);
            xInertiaVelocity = xVelocity - xMaxSpeed * sign;
            xVelocity = xMaxSpeed * sign;
        }
        xInertiaVelocity += xOuterInertia;
        //Debug.Log("Break" + xInertiaVelocity);

        yVelocity = (yVelocity + (yInertiaVelocity > 0 ? yInertiaVelocity : 0)) * yGloveEndMultipiler;
        
        if(yVelocity > yJumpVelocity)
        {
            yVelocity = yJumpVelocity + CalculateYInertiaPixel(yVelocity-yJumpVelocity);
        }

        //if (yVelocity > yJumpVelocity)
        //{

        //}
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
    public void HandleGlove()
    {
        if( gloveInputBuffer == gloveTolerantFrame)
            gloveDecidedDirection = GetDirection();
        if (gloveInputBuffer > 0)
        {
            UseGlove(gloveDecidedDirection);
        }
        //prevent first frame cast, because the direction is not decided.
        //else if (gloveInputBuffer == gloveTolerantFrame - 1)
        //    gloveDecidedDirection = GetDirection();
        gloveInputBuffer--;
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
        #region Glove
        if(Input.GetKeyDown(KeyCode.C))
        {
            GamePhysics.EngineStop(gloveStopTimeFrame);
            gloveInputBuffer = gloveTolerantFrame;
        }
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
        }
    }
    public void SolidDestroyed(Solid solid)
    {
        if (gloveDashing && GetRidingSolid() == solid)
            BreakGlove();
    }
    #endregion
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
                if (sign < 0 ? !CheckSolidBelow() : !CheckSolidAbove())
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

        //Debug.Log(xRemainder);
        position.x = 280;
        position.y = 140;
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
    }
}
