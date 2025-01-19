using System;
using UnityEngine;

/// <summary>
/// Component attached to each rat so that clicking a Rat selects the horde it belongs to
/// </summary>
public class RatController : MonoBehaviour
{
    public Sprite DirectionUp;
    public Sprite DirectionUpLeft;
    public Sprite DirectionUpRight;
    public Sprite DirectionLeft;
    public Sprite DirectionRight;
    public Sprite DirectionDownLeft;
    public Sprite DirectionDown;
    public Sprite DirectionDownRight;

    private SpriteRenderer _spriteRenderer;

    private Rigidbody2D _rigidbody;

    private HordeController _hordeController;

    private byte _currentIntraHordeTarget = 0;
    /// <summary>
    /// Cycles 0,1,2,3 if true, or 3,2,1,0 if false
    /// </summary>
    private bool _cycleIntraHordeTargetForwards = true;

    public void SetHordeController(HordeController controller)
    {
        _hordeController = controller;
    }
    
    public void Start()
    {
        _cycleIntraHordeTargetForwards = UnityEngine.Random.Range(0.0f, 1.0f) > 0.5f;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.mass = UnityEngine.Random.Range(0.8f, 1.2f);
    }

    void Update()
    {
        float angle = Vector2.SignedAngle(transform.up, Vector2.up);
        
        // Normalise to clockwise
        if (angle < 0)
        {
            angle += 360f;
        }

        if (angle < 22.5f)
        {
            _spriteRenderer.sprite = DirectionUp;
        } else if (angle < 67.5)
        {
            _spriteRenderer.sprite = DirectionUpRight;
        } else if (angle < 112.5)
        {
            _spriteRenderer.sprite = DirectionRight;
        } else if (angle < 157.5)
        {
            _spriteRenderer.sprite = DirectionDownRight;
        } else if (angle < 202.5)
        {
            _spriteRenderer.sprite = DirectionDown;
        } else if (angle < 247.5)
        {
            _spriteRenderer.sprite = DirectionDownLeft;
        } else if (angle < 292.5)
        {
            _spriteRenderer.sprite = DirectionLeft;
        }
        else
        {
            _spriteRenderer.sprite = DirectionUpLeft;
        }
        _spriteRenderer.transform.localRotation = Quaternion.Euler(new Vector3(0,0,angle));
    }
    
    private void OnMouseDown()
    {
        HordeController hordeController = GetComponentInParent<HordeController>();
        HumanPlayer player = hordeController.GetComponentInParent<HumanPlayer>();
        player.SelectHorde(hordeController);
    }
    
    /// <param name="force">The force to apply to the rat</param>
    /// <returns>New velocity</returns>
    public Vector2 _addForce(Vector2 force)
    {
        _rigidbody.AddForce(force);
        _rigidbody.linearVelocity = Vector2.ClampMagnitude(_rigidbody.linearVelocity, 0.6f);
        return _rigidbody.linearVelocity;
    }

    public Bounds GetBounds()
    {
        return _spriteRenderer.bounds;
    }

    private void FixedUpdate()
    {
        if (((Vector2)transform.position - _hordeController.intraHordeTargets[_currentIntraHordeTarget]).magnitude < _hordeController.targetTolerance)
        {
            if (_cycleIntraHordeTargetForwards)
            {
                if (_currentIntraHordeTarget == 3)
                {
                    _currentIntraHordeTarget = 0;
                }
                else
                {
                    _currentIntraHordeTarget++;
                }
            }
            else
            {
                if (_currentIntraHordeTarget == 0)
                {
                    _currentIntraHordeTarget = 3;
                }
                else
                {
                    _currentIntraHordeTarget--;
                }
            }
        }
        
        // Get desired direction
        Vector2 direction = _hordeController.intraHordeTargets[_currentIntraHordeTarget] - (Vector2)transform.position;
        // Get desired rotation from desired direction
        Quaternion targetRotation = Quaternion.LookRotation(forward: Vector3.forward, upwards: direction);
            
        // If the rat is facing exactly away from target, then Unity fumbles the calculation, so just offset the target
        // https://discussions.unity.com/t/longest-distance-rotation-problem-from-175-to-175-via-0-using-quaternion-rotatetowards-stops-at-5-why/103817
        if (Quaternion.Inverse(targetRotation) == transform.rotation)
        {
            float degrees = targetRotation.eulerAngles.z;
            targetRotation = Quaternion.Euler(0, 0, degrees + 90);
        }
        // Lerp to desired rotation
        Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 1440 * Time.deltaTime);
        // Apply rotation to current direction
        Vector2 headingIn = newRotation * Vector2.up;
        // Push rat in new direction and get current direction
        Vector2 currentDirection = _addForce(headingIn.normalized);
        // Turn rat to face current direction
        Quaternion currentRotation = Quaternion.LookRotation(forward: Vector3.forward, upwards: currentDirection);
        transform.rotation = currentRotation;
    }
}
