using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Media.Imaging;

namespace RedefinedVehicleImporter
{
    internal sealed class NativeVehiclePreview : Border
    {
        private readonly Viewport3D _viewport = new Viewport3D();
        private readonly PerspectiveCamera _camera = new PerspectiveCamera();
        private readonly Model3DGroup _scene = new Model3DGroup();
        private Point _dragStart;
        private double _yaw = 0.65;
        private double _pitch = -0.20;
        private double _distance = 8;

        public NativeVehiclePreview()
        {
            Background = new SolidColorBrush(Color.FromRgb(8, 15, 24));
            Child = _viewport;
            Focusable = true;
            Cursor = System.Windows.Input.Cursors.SizeAll;
            // Viewport3D otherwise becomes the input source and can consume pointer movement.
            // The Border owns all orbit/zoom interaction and forwards camera changes to it.
            _viewport.IsHitTestVisible = false;
            _camera.FieldOfView = 48;
            _viewport.Camera = _camera;
            _scene.Children.Add(new AmbientLight(Color.FromRgb(65, 75, 90)));
            _scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-0.45, -0.7, -1)));
            _scene.Children.Add(new DirectionalLight(Color.FromRgb(50, 120, 150), new Vector3D(0.7, 0.25, -0.4)));
            _viewport.Children.Add(new ModelVisual3D { Content = _scene });
            PreviewMouseDown += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e)
            {
                if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
                Focus(); _dragStart = e.GetPosition(this);
                if (CaptureMouse()) e.Handled = true;
            };
            PreviewMouseUp += delegate(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.ChangedButton == System.Windows.Input.MouseButton.Left && IsMouseCaptured) { ReleaseMouseCapture(); e.Handled = true; } };
            LostMouseCapture += delegate { Cursor = System.Windows.Input.Cursors.SizeAll; };
            PreviewMouseMove += delegate(object sender, System.Windows.Input.MouseEventArgs e)
            {
                if (!IsMouseCaptured || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
                var p = e.GetPosition(this); Rotate((p.X - _dragStart.X) * .018, (p.Y - _dragStart.Y) * .012); _dragStart = p; e.Handled = true;
            };
            PreviewMouseWheel += delegate(object sender, System.Windows.Input.MouseWheelEventArgs e) { Zoom(e.Delta > 0); e.Handled = true; };
        }

        public void ResetCamera()
        {
            _yaw = 0.65;
            _pitch = -0.20;
            _distance = 8;
            UpdateCamera();
        }

        public void Zoom(bool closer)
        {
            _distance = Math.Max(1.2, Math.Min(80, _distance * (closer ? .82 : 1.22)));
            UpdateCamera();
        }

        public void Rotate(double horizontal, double vertical)
        {
            _yaw = NormalizeYaw(_yaw + horizontal);
            _pitch = Math.Max(-1.35, Math.Min(1.35, _pitch + vertical));
            UpdateCamera();
        }

        public void SetView(double yaw, double pitch)
        {
            _yaw = NormalizeYaw(yaw);
            _pitch = Math.Max(-1.35, Math.Min(1.35, pitch));
            UpdateCamera();
        }

        private static double NormalizeYaw(double yaw) { var fullTurn = Math.PI * 2; yaw %= fullTurn; return yaw < 0 ? yaw + fullTurn : yaw; }

        public async Task LoadAsync(string archivePath, string modelName, CancellationToken cancellationToken, IProgress<string> progress)
        {
            progress.Report("Reading native vehicle archive...");
            var files = await Task.Run(() => new NativeVehicleArchive().ExtractVehicle(archivePath, modelName), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report("Decoding YFT meshes...");
            var decoded = await Task.Run(() => Decode(files, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Action render = delegate
            {
                Rebuild(decoded.Meshes, decoded.Textures);
                _distance = Math.Max(4, decoded.Radius * 2.8);
                UpdateCamera();
            };
            if (Dispatcher.CheckAccess()) render(); else Dispatcher.Invoke(render);
            progress.Report("Rendered " + decoded.Meshes.Count + " native mesh section(s) with " + (decoded.Textures == null ? 0 : decoded.Textures.Count) + " decoded texture(s).");
        }

        private void Rebuild(List<NativeMeshData> meshes, Dictionary<uint, BitmapSource> textures)
        {
            while (_scene.Children.Count > 3) _scene.Children.RemoveAt(_scene.Children.Count - 1);
            foreach (var mesh in meshes) _scene.Children.Add(BuildModel(mesh, textures));
        }

        private sealed class NativePreviewData { public List<NativeMeshData> Meshes; public Dictionary<uint, BitmapSource> Textures; public double Radius; }
        private static NativePreviewData Decode(NativeVehicleArchive.VehicleFiles files, CancellationToken token)
        {
            var reader = new NativeResourceReader(files.Yft, files.YftSystemFlags, files.YftGraphicsFlags);
            var drawable = NativeDrawableInfo.Read(reader, NativeYftHeader.Read(reader).DrawablePointer);
            var result = new List<NativeMeshData>();
            foreach (var geometry in NativeMeshManifest.ReadHighDetail(reader, drawable))
            {
                token.ThrowIfCancellationRequested();
                if (geometry.VertexCount == 0 || geometry.IndexCount < 3) continue;
                try
                {
                    var raw = NativeMeshDecoder.Decode(reader, geometry);
                    if (raw.Indices.Length >= 3) { raw.TextureHash = geometry.TextureHash; result.Add(raw); }
                }
                catch (InvalidDataException) { }
            }
            if (result.Count == 0) throw new InvalidDataException("No renderable meshes could be decoded from the selected YFT.");
            var requiredTextures = new HashSet<uint>(); foreach (var mesh in result) if (mesh.TextureHash != 0) requiredTextures.Add(mesh.TextureHash);
            return new NativePreviewData { Meshes = result, Textures = NativeTextureDecoder.ReadTextures(files.Ytds, requiredTextures), Radius = Math.Max(1, drawable.Radius) };
        }

        private static Model3D BuildModel(NativeMeshData raw, Dictionary<uint, BitmapSource> textures)
        {
            var mesh = new MeshGeometry3D();
            for (var i = 0; i < raw.Positions.Length; i += 3) mesh.Positions.Add(new Point3D(raw.Positions[i], raw.Positions[i + 1], raw.Positions[i + 2]));
            if (raw.TextureCoordinates != null) for (var i = 0; i < raw.TextureCoordinates.Length; i += 2) mesh.TextureCoordinates.Add(new Point(raw.TextureCoordinates[i], 1 - raw.TextureCoordinates[i + 1]));
            for (var i = 0; i + 2 < raw.Indices.Length; i += 3)
                if (raw.Indices[i] < mesh.Positions.Count && raw.Indices[i + 1] < mesh.Positions.Count && raw.Indices[i + 2] < mesh.Positions.Count) { mesh.TriangleIndices.Add(raw.Indices[i]); mesh.TriangleIndices.Add(raw.Indices[i + 1]); mesh.TriangleIndices.Add(raw.Indices[i + 2]); }
            BitmapSource texture = null; if (raw.TextureHash != 0 && textures != null) textures.TryGetValue(raw.TextureHash, out texture); var image = texture;
            Material material = image == null || raw.TextureCoordinates == null ? new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(188, 205, 219))) : new DiffuseMaterial(new ImageBrush(image) { Stretch = Stretch.Fill });
            return new GeometryModel3D(mesh, material) { BackMaterial = material };
        }

        private void UpdateCamera()
        {
            var x = _distance * Math.Cos(_pitch) * Math.Sin(_yaw);
            var y = _distance * Math.Sin(_pitch);
            var z = _distance * Math.Cos(_pitch) * Math.Cos(_yaw);
            _camera.Position = new Point3D(x, y, z);
            _camera.LookDirection = new Vector3D(-x, -y, -z);
            _camera.UpDirection = new Vector3D(0, 1, 0);
        }
    }
}
