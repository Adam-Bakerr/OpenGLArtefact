using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace OpenTkVoxelEngine
{
    internal class Camera
    {
        //Reference to the active window
        private GameWindow _window;

        //Camera Matrices
        private Matrix4 _projection = Matrix4.Identity;

        public Matrix4 Projection() => _projection;

        private Matrix4 _model = Matrix4.Identity;

        public Matrix4 Model() => _model;

        private Matrix4 _view = Matrix4.Identity;

        public Matrix4 View() => _view;

        //Camera Variables
        private float _FOV = 45.0f;

        public float FOV() => _FOV;

        private float _maxFOV = 45.0f;
        private float _maxSpeed = 50000.0f;
        private float _nearPlane = .1f;
        private float _farPlane = 100f;
        private float _speed = 12f;
        private float _sensitivity = 1.0f;

        private Vector3 _position = Vector3.Zero;
        private Vector3 _front = Vector3.UnitZ;
        private Vector3 _up = Vector3.UnitY;
        private Vector3 _right = Vector3.UnitX;

        public Vector3 Up() => _up;

        public Vector3 Forward() => _front;

        public Vector3 Right() => _right;

        private Vector2 lastPos;

        private float _yaw = 90;
        private float _pitch = 0;

        private bool _cursorLocked = true;
        private bool _firstMove = true;

        public Camera(GameWindow window, float nearPlane, float farPlane)
        {
            _window = window;
            _nearPlane = nearPlane;
            _farPlane = farPlane;

            //Create default matrix
            _model = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-55.0f));
            _view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(_FOV), (float)_window.Size.X / (float)_window.Size.Y, _nearPlane, _farPlane);

            //Lock the cusror
            _window.CursorState = CursorState.Grabbed;

            _window.MouseWheel += OnMouseWheel;
        }

        public Camera(GameWindow window, float nearPlane, float farPlane, Vector3 position)
        {
            _window = window;
            _nearPlane = nearPlane;
            _farPlane = farPlane;
            _position = position;

            //Create default matrix
            _model = Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-55.0f));
            _view = Matrix4.CreateTranslation(0.0f, 0.0f, -3.0f);
            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(_FOV), (float)_window.Size.X / (float)_window.Size.Y, _nearPlane, _farPlane);

            //Lock the cusror
            _window.CursorState = CursorState.Grabbed;
        }

        public Vector3 Position() => _position;

        public void OnUpdateFrame(FrameEventArgs args)
        {
            ListenForInput(args);

            Look(args);
        }

        private void Look(FrameEventArgs args)
        {
            //Stop mouse input when locked
            if (!_cursorLocked) return;

            var mouse = _window.MouseState;
            if (_firstMove)
            {
                lastPos = new Vector2(mouse.X, mouse.Y);
                _firstMove = false;
                return;
            }
            //Capture the mouse delta each frame

            var deltaX = mouse.X - lastPos.X;
            var deltaY = mouse.Y - lastPos.Y;

            lastPos = new Vector2(mouse.X, mouse.Y);

            //Clamp Pitch
            if (_pitch + deltaY > 89.0f)
            {
                _pitch = 88.0f;
            }
            else if (_pitch - deltaY < -89.0f)
            {
                _pitch = -88.0f;
            }
            else
            {
                _pitch -= deltaY * _sensitivity;
            }
            _yaw += deltaX * _sensitivity;

            _front.X = (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Cos(MathHelper.DegreesToRadians(_yaw));
            _front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
            _front.Z = (float)Math.Cos(MathHelper.DegreesToRadians(_pitch)) * (float)Math.Sin(MathHelper.DegreesToRadians(_yaw));

            _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY)).Normalized();

            _up = Vector3.Cross(_right, _front).Normalized();

            _view = GetViewModel();
        }

        public Matrix4 GetViewModel()
        {
            return Matrix4.LookAt(_position, _position + _front, _up);
        }

        private void ListenForInput(FrameEventArgs args)
        {
            //Handles the movement
            if (_window.IsKeyDown(Keys.W))
            {
                _position += _front * _speed * (float)args.Time; //Forward
            }

            if (_window.IsKeyDown(Keys.S))
            {
                _position -= _front * _speed * (float)args.Time; //Backwards
            }

            if (_window.IsKeyDown(Keys.A))
            {
                _position -= Vector3.Normalize(Vector3.Cross(_front, _up)) * _speed * (float)args.Time; //Left
            }

            if (_window.IsKeyDown(Keys.D))
            {
                _position += Vector3.Normalize(Vector3.Cross(_front, _up)) * _speed * (float)args.Time; //Right
            }

            if (_window.IsKeyDown(Keys.Space))
            {
                _position += _up * _speed * (float)args.Time; //Up
            }

            if (_window.IsKeyDown(Keys.LeftShift))
            {
                _position -= _up * _speed * (float)args.Time; //Down
            }

            //Handle closing the window and repositions mouse
            if (_window.IsKeyDown(Keys.LeftAlt) && _cursorLocked == true)
            {
                _cursorLocked = false;
            }
            else if (_cursorLocked == false && !_window.IsKeyDown(Keys.LeftAlt))
            {
                _cursorLocked = true;
                _firstMove = true;
            }
        }

        //Handles the zooming
        public void OnMouseWheel(MouseWheelEventArgs e)
        {
            _speed = Math.Clamp(_speed + e.OffsetY, 0.15f, _maxSpeed);

            _projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(_FOV), (float)_window.Size.X / (float)_window.Size.Y, _nearPlane, _farPlane);
        }

        public void OnUnload()
        {
        }
    }
}