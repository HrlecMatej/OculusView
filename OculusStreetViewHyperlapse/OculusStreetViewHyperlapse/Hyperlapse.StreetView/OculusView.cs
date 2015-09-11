using System;
using System.Diagnostics;
using System.Windows.Forms;
using OculusWrap;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Threading;
using System.Threading;
using System.Windows.Input;

namespace OculusStreetViewHyperlapse.Hyperlapse.StreetView
{
    partial class OculusView : DependencyObject
    {
        /*public Stream streamTexture
        {
            get
            {
                return (Stream)GetValue(StreamProperty);
            }
            set
            {
                SetValue(StreamProperty, value);
            }
        }*/

        public Stream streamTexture = null;

        private int zoom;
        public bool newTextureArrived = false;
        public bool oculusReady = false;

        //public SharpDX.WIC.BitmapSource bitMap = null;
        //RenderForm form;
        private StreetView streetView;


        public OculusView(int zoom, StreetView streetView)
        {
            //InitializeComponent();
            this.streetView = streetView;
            this.oculusReady = false;
            this.zoom = zoom;

            //InitializeOculus();

            //System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { InitializeOculus(); }));
            //System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { InitializeOculus(); }));
            //Task task = startIt();
            //task.Start();
            Task.Factory.StartNew(() => startIt().ContinueWith(t => t.Wait(), TaskContinuationOptions.AttachedToParent));
            //await newTask;
        }

        async private Task startIt () {
            InitializeOculus();
            Debug.WriteLine("huehuehue");
        }

        private void Window_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (e.KeyCode == Keys.H)
            {
                System.Windows.MessageBox.Show("Street View commands:\n\nKey Space to pause/resume,\nKey L to loop ,\nKeys Up and Down to move by one jump,\nKey R to restart playing.",
                    "Help", MessageBoxButton.OK, MessageBoxImage.Question);
            }
            else if (e.KeyCode == Keys.Space)
            {
                streetView.pause = !streetView.pause;
                streetView.moveForwards = false;
                streetView.moveBackwards = false;
            }
            else if (e.KeyCode == Keys.L)
            {
                if (streetView.loopWhole == true)
                {
                    streetView.loopWhole = false;
                }
                else if (streetView.loopWhole == false)
                {
                    streetView.loopWhole = true;
                }
            }
            else if (e.KeyCode == Keys.Up)
            {
                streetView.pause = true;
                streetView.moveForwards = true;
                streetView.moveBackwards = false;
            }
            else if (e.KeyCode == Keys.Down)
            {
                streetView.pause = true;
                streetView.moveBackwards = true;
                streetView.moveForwards = false;
            }
            else if (e.KeyCode == Keys.R)
            {
                streetView.restart = true;

                streetView.pause = false;
                streetView.moveForwards = false;
                streetView.moveBackwards = false;

                streetView.playForward = true;
            }
        }

        private void InitializeOculus()
        {
            RenderForm form = new RenderForm("OculusWrap SharpDX demo");

			Wrap	oculus	= new Wrap();
			Hmd		hmd;

            form.KeyUp += new System.Windows.Forms.KeyEventHandler(this.Window_KeyUp);

            //form.moused

            //form.Activate();
            //form.Show();

            int textureWidth = 0, textureHeight = 0;
            newTextureArrived = false;

            //zoom == 2 is not implemented, because the visual quality would be too low.
            //zoom == 4 will be implemented in the future.
            if (zoom == 3)
            {
                textureWidth = 3328;
                textureHeight = 1664;
            }

            bool success = oculus.Initialize();
            if (!success)
            {
                System.Windows.Forms.MessageBox.Show("Failed to initialize the Oculus runtime library.", "Uh oh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Use the head mounted display, if it's available, otherwise use the debug HMD.
            int numberOfHeadMountedDisplays = oculus.Hmd_Detect();
            if (numberOfHeadMountedDisplays > 0)
                hmd = oculus.Hmd_Create(0);
            else
                hmd = oculus.Hmd_CreateDebug(OculusWrap.OVR.HmdType.DK2);

            if (hmd == null)
            {
                System.Windows.Forms.MessageBox.Show("Oculus Rift not detected.", "Uh oh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (hmd.ProductName == string.Empty)
                System.Windows.Forms.MessageBox.Show("The HMD is not enabled.", "There's a tear in the Rift", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Specify which head tracking capabilities to enable.
            hmd.SetEnabledCaps(OVR.HmdCaps.LowPersistence | OVR.HmdCaps.DynamicPrediction);

            // Start the sensor which informs of the Rift's pose and motion
            hmd.ConfigureTracking(OVR.TrackingCaps.ovrTrackingCap_Orientation | OVR.TrackingCaps.ovrTrackingCap_MagYawCorrection | OVR.TrackingCaps.ovrTrackingCap_Position, OVR.TrackingCaps.None);

            // Create a set of layers to submit.
            EyeTexture[] eyeTextures = new EyeTexture[2];
            OVR.ovrResult result;

            // Create DirectX drawing device.
            SharpDX.Direct3D11.Device device = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.Debug);

            // Create DirectX Graphics Interface factory, used to create the swap chain.
            Factory factory = new Factory();

            DeviceContext immediateContext = device.ImmediateContext;

            // Define the properties of the swap chain.
            SwapChainDescription swapChainDescription = new SwapChainDescription();
            swapChainDescription.BufferCount = 1;
            swapChainDescription.IsWindowed = true;
            swapChainDescription.OutputHandle = form.Handle;
            swapChainDescription.SampleDescription = new SampleDescription(1, 0);
            swapChainDescription.Usage = Usage.RenderTargetOutput | Usage.ShaderInput;
            swapChainDescription.SwapEffect = SwapEffect.Sequential;
            swapChainDescription.Flags = SwapChainFlags.AllowModeSwitch;
            swapChainDescription.ModeDescription.Width = form.Width;
            swapChainDescription.ModeDescription.Height = form.Height;
            swapChainDescription.ModeDescription.Format = Format.R8G8B8A8_UNorm;
            swapChainDescription.ModeDescription.RefreshRate.Numerator = 0;
            swapChainDescription.ModeDescription.RefreshRate.Denominator = 1;

            // Create the swap chain.
            SharpDX.DXGI.SwapChain swapChain = new SwapChain(factory, device, swapChainDescription);

            // Retrieve the back buffer of the swap chain.
            Texture2D backBuffer = swapChain.GetBackBuffer<Texture2D>(0);
            RenderTargetView backBufferRenderTargetView = new RenderTargetView(device, backBuffer);

            // Create a depth buffer, using the same width and height as the back buffer.
            Texture2DDescription depthBufferDescription = new Texture2DDescription();
            depthBufferDescription.Format = Format.D32_Float;
            depthBufferDescription.ArraySize = 1;
            depthBufferDescription.MipLevels = 1;
            depthBufferDescription.Width = form.Width;
            depthBufferDescription.Height = form.Height;
            depthBufferDescription.SampleDescription = new SampleDescription(1, 0);
            depthBufferDescription.Usage = ResourceUsage.Default;
            depthBufferDescription.BindFlags = BindFlags.DepthStencil;
            depthBufferDescription.CpuAccessFlags = CpuAccessFlags.None;
            depthBufferDescription.OptionFlags = ResourceOptionFlags.None;

            // Define how the depth buffer will be used to filter out objects, based on their distance from the viewer.
            DepthStencilStateDescription depthStencilStateDescription = new DepthStencilStateDescription();
            depthStencilStateDescription.IsDepthEnabled = true;
            depthStencilStateDescription.DepthComparison = Comparison.Less;
            depthStencilStateDescription.DepthWriteMask = DepthWriteMask.Zero;

            // Create the depth buffer.
            Texture2D depthBuffer = new Texture2D(device, depthBufferDescription);
            DepthStencilView depthStencilView = new DepthStencilView(device, depthBuffer);
            DepthStencilState depthStencilState = new DepthStencilState(device, depthStencilStateDescription);
            Viewport viewport = new Viewport(0, 0, hmd.Resolution.Width, hmd.Resolution.Height, 0.0f, 1.0f);

            immediateContext.OutputMerger.SetDepthStencilState(depthStencilState);
            immediateContext.OutputMerger.SetRenderTargets(depthStencilView, backBufferRenderTargetView);
            immediateContext.Rasterizer.SetViewport(viewport);

            // Retrieve the DXGI device, in order to set the maximum frame latency.
            using (SharpDX.DXGI.Device1 dxgiDevice = device.QueryInterface<SharpDX.DXGI.Device1>())
            {
                dxgiDevice.MaximumFrameLatency = 1;
            }

            Layers layers = new Layers();
            LayerEyeFov layerEyeFov = layers.AddLayerEyeFov();

            for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
            {
                OVR.EyeType eye = (OVR.EyeType)eyeIndex;
                EyeTexture eyeTexture = new EyeTexture();
                eyeTextures[eyeIndex] = eyeTexture;

                // Retrieve size and position of the texture for the current eye.
                eyeTexture.FieldOfView = hmd.DefaultEyeFov[eyeIndex];
                eyeTexture.TextureSize = hmd.GetFovTextureSize(eye, hmd.DefaultEyeFov[eyeIndex], 1.0f);
                eyeTexture.RenderDescription = hmd.GetRenderDesc(eye, hmd.DefaultEyeFov[eyeIndex]);
                eyeTexture.HmdToEyeViewOffset = eyeTexture.RenderDescription.HmdToEyeViewOffset;
                eyeTexture.ViewportSize.Position = new OVR.Vector2i(0, 0);
                eyeTexture.ViewportSize.Size = eyeTexture.TextureSize;
                eyeTexture.Viewport = new Viewport(0, 0, eyeTexture.TextureSize.Width, eyeTexture.TextureSize.Height, 0.0f, 1.0f);

                // Define a texture at the size recommended for the eye texture.
                eyeTexture.Texture2DDescription = new Texture2DDescription();
                eyeTexture.Texture2DDescription.Width = eyeTexture.TextureSize.Width;
                eyeTexture.Texture2DDescription.Height = eyeTexture.TextureSize.Height;
                eyeTexture.Texture2DDescription.ArraySize = 1;
                eyeTexture.Texture2DDescription.MipLevels = 1;
                eyeTexture.Texture2DDescription.Format = Format.R8G8B8A8_UNorm;
                eyeTexture.Texture2DDescription.SampleDescription = new SampleDescription(1, 0);
                eyeTexture.Texture2DDescription.Usage = ResourceUsage.Default;
                eyeTexture.Texture2DDescription.CpuAccessFlags = CpuAccessFlags.None;
                eyeTexture.Texture2DDescription.BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget;

                // Convert the SharpDX texture description to the native Direct3D texture description.
                OVR.D3D11.D3D11_TEXTURE2D_DESC swapTextureDescriptionD3D11 = SharpDXHelpers.CreateTexture2DDescription(eyeTexture.Texture2DDescription);

                // Create a SwapTextureSet, which will contain the textures to render to, for the current eye.
                result = hmd.CreateSwapTextureSetD3D11(device.NativePointer, ref swapTextureDescriptionD3D11, out eyeTexture.SwapTextureSet);
                WriteErrorDetails(oculus, result, "Failed to create swap texture set.");

                // Create room for each DirectX texture in the SwapTextureSet.
                eyeTexture.Textures = new Texture2D[eyeTexture.SwapTextureSet.TextureCount];
                eyeTexture.RenderTargetViews = new RenderTargetView[eyeTexture.SwapTextureSet.TextureCount];

                // Create a texture 2D and a render target view, for each unmanaged texture contained in the SwapTextureSet.
                for (int textureIndex = 0; textureIndex < eyeTexture.SwapTextureSet.TextureCount; textureIndex++)
                {
                    // Retrieve the current textureData object.
                    OVR.D3D11.D3D11TextureData textureData = eyeTexture.SwapTextureSet.Textures[textureIndex];

                    // Create a managed Texture2D, based on the unmanaged texture pointer.
                    eyeTexture.Textures[textureIndex] = new Texture2D(textureData.Texture);

                    // Create a render target view for the current Texture2D.
                    eyeTexture.RenderTargetViews[textureIndex] = new RenderTargetView(device, eyeTexture.Textures[textureIndex]);
                }

                // Define the depth buffer, at the size recommended for the eye texture.
                eyeTexture.DepthBufferDescription = new Texture2DDescription();
                eyeTexture.DepthBufferDescription.Format = Format.D32_Float;
                eyeTexture.DepthBufferDescription.Width = eyeTexture.TextureSize.Width;
                eyeTexture.DepthBufferDescription.Height = eyeTexture.TextureSize.Height;
                eyeTexture.DepthBufferDescription.ArraySize = 1;
                eyeTexture.DepthBufferDescription.MipLevels = 1;
                eyeTexture.DepthBufferDescription.SampleDescription = new SampleDescription(1, 0);
                eyeTexture.DepthBufferDescription.Usage = ResourceUsage.Default;
                eyeTexture.DepthBufferDescription.BindFlags = BindFlags.DepthStencil;
                eyeTexture.DepthBufferDescription.CpuAccessFlags = CpuAccessFlags.None;
                eyeTexture.DepthBufferDescription.OptionFlags = ResourceOptionFlags.None;

                // Create the depth buffer.
                eyeTexture.DepthBuffer = new Texture2D(device, eyeTexture.DepthBufferDescription);
                eyeTexture.DepthStencilView = new DepthStencilView(device, eyeTexture.DepthBuffer);

                // Specify the texture to show on the HMD.
                layerEyeFov.ColorTexture[eyeIndex] = eyeTexture.SwapTextureSet.SwapTextureSetPtr;
                layerEyeFov.Viewport[eyeIndex].Position = new OVR.Vector2i(0, 0);
                layerEyeFov.Viewport[eyeIndex].Size = eyeTexture.TextureSize;
                layerEyeFov.Fov[eyeIndex] = eyeTexture.FieldOfView;
                layerEyeFov.Header.Flags = OVR.LayerFlags.TextureOriginAtBottomLeft;
            }

            // Define the texture used to display the rendered result on the computer monitor.
            Texture2DDescription mirrorTextureDescription = new Texture2DDescription();
            mirrorTextureDescription.Width = form.Width;
            mirrorTextureDescription.Height = form.Height;
            mirrorTextureDescription.ArraySize = 1;
            mirrorTextureDescription.MipLevels = 1;
            mirrorTextureDescription.Format = Format.R8G8B8A8_UNorm;
            mirrorTextureDescription.SampleDescription = new SampleDescription(1, 0);
            mirrorTextureDescription.Usage = ResourceUsage.Default;
            mirrorTextureDescription.CpuAccessFlags = CpuAccessFlags.None;
            mirrorTextureDescription.BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget;

            SamplerStateDescription samplerStateDescription = new SamplerStateDescription
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                Filter = Filter.Anisotropic
            };

            RasterizerStateDescription rasterizerStateDescription = RasterizerStateDescription.Default();
            rasterizerStateDescription.IsFrontCounterClockwise = true;

            // Convert the SharpDX texture description to the native Direct3D texture description.
            OVR.D3D11.D3D11_TEXTURE2D_DESC mirrorTextureDescriptionD3D11 = SharpDXHelpers.CreateTexture2DDescription(mirrorTextureDescription);

            OculusWrap.D3D11.MirrorTexture mirrorTexture;

            // Create the texture used to display the rendered result on the computer monitor.
            result = hmd.CreateMirrorTextureD3D11(device.NativePointer, ref mirrorTextureDescriptionD3D11, out mirrorTexture);
            WriteErrorDetails(oculus, result, "Failed to create mirror texture.");

            Texture2D mirrorTextureD3D11 = new Texture2D(mirrorTexture.Texture.Texture);

            #region Vertex and pixel shader
            // Create vertex shader.
            ShaderBytecode vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.fx", "VertexShaderMain", "vs_4_0");
            VertexShader vertexShader = new VertexShader(device, vertexShaderByteCode);

            // Create pixel shader.
            ShaderBytecode pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.fx", "PixelShaderMain", "ps_4_0");
            PixelShader pixelShader = new PixelShader(device, pixelShaderByteCode);

            ShaderSignature shaderSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);



            Texture2D myTexture = new Texture2D(device, new Texture2DDescription()
            {
                Format = Format.R8G8B8A8_UNorm,
                ArraySize = 1,
                MipLevels = 1,
                Width = textureWidth,
                Height = textureHeight,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
            });


            ShaderResourceView textureView = new ShaderResourceView(device, myTexture);


            //set sampler for texture
            SamplerState samplerState = new SamplerState(device, samplerStateDescription);

            //initialize rasterizer
            RasterizerState rasterizerState = new RasterizerState(device, rasterizerStateDescription);

            // Specify that each vertex consists of a single vertex position and color.

            int[] indices = null;
            Vertex[] vertices = null;
            CreateGeometry(out indices, out vertices);

            InputElement[] inputElements = new InputElement[]
            {
                new InputElement("SV_Position", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 32, 0),
                /*new InputElement("TEXCOORD", 0, Format.R32G32_Float, 16, 0),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 32, 0),*/
            };

            // Define an input layout to be passed to the vertex shader.
            InputLayout inputLayout = new InputLayout(device, shaderSignature, inputElements);

            // Create a vertex buffer, containing our 3D model.
            Buffer vertexBuffer = Buffer.Create(device, BindFlags.VertexBuffer, vertices);//m_vertices);

            // Create a constant buffer, to contain our WorldViewProjection matrix, that will be passed to the vertex shader.
            Buffer constantBuffer = new Buffer(device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            Buffer indexBuffer = SharpDX.Direct3D11.Buffer.Create(device, BindFlags.IndexBuffer, indices);

            // Setup the immediate context to use the shaders and model we defined.
            immediateContext.InputAssembler.InputLayout = inputLayout;
            immediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            immediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<Vertex>(), 0));
            immediateContext.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, 0);

            immediateContext.VertexShader.SetConstantBuffer(0, constantBuffer);

            immediateContext.VertexShader.Set(vertexShader);
            immediateContext.PixelShader.Set(pixelShader);

            immediateContext.PixelShader.SetShaderResource(0, textureView);
            immediateContext.PixelShader.SetSampler(0, samplerState);
            #endregion

            DateTime startTime = DateTime.Now;
            Vector3 position = new Vector3(0, 0, 0);

            oculusReady = true;

            #region Render loop
            RenderLoop.Run(form, () =>
            {
                OVR.Vector3f[] hmdToEyeViewOffsets = { eyeTextures[0].HmdToEyeViewOffset, eyeTextures[1].HmdToEyeViewOffset };
                OVR.FrameTiming frameTiming = hmd.GetFrameTiming(0);
                OVR.TrackingState trackingState = hmd.GetTrackingState(frameTiming.DisplayMidpointSeconds);
                OVR.Posef[] eyePoses = new OVR.Posef[2];

                // Calculate the position and orientation of each eye.
                oculus.CalcEyePoses(trackingState.HeadPose.ThePose, hmdToEyeViewOffsets, ref eyePoses);

                float timeSinceStart = (float)(DateTime.Now - startTime).TotalSeconds;

                for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
                {
                    OVR.EyeType eye = (OVR.EyeType)eyeIndex;
                    EyeTexture eyeTexture = eyeTextures[eyeIndex];

                    layerEyeFov.RenderPose[eyeIndex] = eyePoses[eyeIndex];

                    // Retrieve the index of the active texture and select the next texture as being active next.
                    int textureIndex = eyeTexture.SwapTextureSet.CurrentIndex++;

                    immediateContext.OutputMerger.SetRenderTargets(eyeTexture.DepthStencilView, eyeTexture.RenderTargetViews[textureIndex]);
                    immediateContext.ClearRenderTargetView(eyeTexture.RenderTargetViews[textureIndex], Color.Black);
                    immediateContext.ClearDepthStencilView(eyeTexture.DepthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
                    immediateContext.Rasterizer.SetViewport(eyeTexture.Viewport);
                    //added a custom rasterizer
                    immediateContext.Rasterizer.State = rasterizerState;

                    // Retrieve the eye rotation quaternion and use it to calculate the LookAt direction and the LookUp direction.
                    Quaternion rotationQuaternion = SharpDXHelpers.ToQuaternion(eyePoses[eyeIndex].Orientation);
                    Matrix rotationMatrix = Matrix.RotationQuaternion(rotationQuaternion);
                    Vector3 lookUp = Vector3.Transform(new Vector3(0, -1, 0), rotationMatrix).ToVector3();
                    Vector3 lookAt = Vector3.Transform(new Vector3(0, 0, 1), rotationMatrix).ToVector3();

                    Vector3 viewPosition = position - eyePoses[eyeIndex].Position.ToVector3();

                    //use this to get the first rotation to goal
                    Matrix world = Matrix.Scaling(1.0f) /** Matrix.RotationX(timeSinceStart*0.2f) */* Matrix.RotationY(timeSinceStart * 2 / 10f) /** Matrix.RotationZ(timeSinceStart*3/10f)*/;
                    Matrix viewMatrix = Matrix.LookAtRH(viewPosition, viewPosition + lookAt, lookUp);

                    Matrix projectionMatrix = OVR.ovrMatrix4f_Projection(eyeTexture.FieldOfView, 0.1f, 10.0f, OVR.ProjectionModifier.None).ToMatrix();
                    projectionMatrix.Transpose();

                    Matrix worldViewProjection = world * viewMatrix * projectionMatrix;
                    worldViewProjection.Transpose();

                    // Update the transformation matrix.
                    immediateContext.UpdateSubresource(ref worldViewProjection, constantBuffer);

                    // Draw the cube
                    //immediateContext.Draw(vertices.Length/2, 0);
                    immediateContext.DrawIndexed(indices.Length, 0, 0);
                }

                hmd.SubmitFrame(0, layers);

                immediateContext.CopyResource(mirrorTextureD3D11, backBuffer);
                swapChain.Present(0, PresentFlags.None);


                if (newTextureArrived == true)
                {
                    newTextureArrived = false;
                    DataBox map = device.ImmediateContext.MapSubresource(myTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);

                    //load the BitMapSource with appropriate formating (Format32bppPRGBA)
                    SharpDX.WIC.BitmapSource bitMap = LoadBitmap(new SharpDX.WIC.ImagingFactory(), streamTexture);
                    //string newFile = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + @"\img_merged.jpg";
                    //SharpDX.WIC.BitmapSource bitMap = LoadBitmapFromFile(new SharpDX.WIC.ImagingFactory(), newFile);

                    int width = bitMap.Size.Width;
                    int height = bitMap.Size.Height;
                    int stride = bitMap.Size.Width * 4;

                    bitMap.CopyPixels(stride, map.DataPointer, height * stride);

                    device.ImmediateContext.UnmapSubresource(myTexture, 0);

                    //bitMap.Dispose();
                    streamTexture.Seek(0, SeekOrigin.Begin);
                }


            });
            #endregion
            // Release all resources
            inputLayout.Dispose();
            constantBuffer.Dispose();
            indexBuffer.Dispose();
            vertexBuffer.Dispose();
            inputLayout.Dispose();
            shaderSignature.Dispose();
            pixelShader.Dispose();
            pixelShaderByteCode.Dispose();
            vertexShader.Dispose();
            vertexShaderByteCode.Dispose();
            mirrorTextureD3D11.Dispose();
            layers.Dispose();
            eyeTextures[0].Dispose();
            eyeTextures[1].Dispose();
            immediateContext.ClearState();
            immediateContext.Flush();
            immediateContext.Dispose();
            depthStencilState.Dispose();
            depthStencilView.Dispose();
            depthBuffer.Dispose();
            backBufferRenderTargetView.Dispose();
            backBuffer.Dispose();
            swapChain.Dispose();
            factory.Dispose();

            // Disposing the device, before the hmd, will cause the hmd to fail when disposing.
            // Disposing the device, after the hmd, will cause the dispose of the device to fail.
            // It looks as if the hmd steals ownership of the device and destroys it, when it's shutting down.
            // device.Dispose();

            hmd.Dispose();
            oculus.Dispose();
        }

        //acknowledges the arrival of new Stream
        /*public static readonly DependencyProperty StreamProperty =
            DependencyProperty.Register("streamTexture", typeof(Stream), typeof(OculusView), new PropertyMetadata(
                  null, new PropertyChangedCallback(OnStreamChanged)));

        internal static void OnStreamChanged(Object sender, DependencyPropertyChangedEventArgs e)
        {
            OculusView oculusView = ((OculusView)sender);
            oculusView.bitMap = LoadBitmap(new SharpDX.WIC.ImagingFactory(), oculusView.streamTexture);
            oculusView.newTextureArrived = true;
        }*/

        public SharpDX.WIC.BitmapSource LoadBitmap(SharpDX.WIC.ImagingFactory factory, Stream streamRef)
        {
            var bitmapDecoder = new SharpDX.WIC.BitmapDecoder(
                factory,
                streamRef,
                SharpDX.WIC.DecodeOptions.CacheOnLoad
                );

            var formatConverter = new SharpDX.WIC.FormatConverter(factory);

            formatConverter.Initialize(
                bitmapDecoder.GetFrame(0),
                SharpDX.WIC.PixelFormat.Format32bppPRGBA,
                SharpDX.WIC.BitmapDitherType.None,
                null,
                0.0,
                SharpDX.WIC.BitmapPaletteType.Custom);

            return formatConverter;
        }

        public static SharpDX.WIC.BitmapSource LoadBitmapFromFile(SharpDX.WIC.ImagingFactory factory, string file)
        {
            var bitmapDecoder = new SharpDX.WIC.BitmapDecoder(
                factory,
                file,
                SharpDX.WIC.DecodeOptions.CacheOnDemand
                );

            var formatConverter = new SharpDX.WIC.FormatConverter(factory);

            formatConverter.Initialize(
                bitmapDecoder.GetFrame(0),
                SharpDX.WIC.PixelFormat.Format32bppPRGBA,
                SharpDX.WIC.BitmapDitherType.None,
                null,
                0.0,
                SharpDX.WIC.BitmapPaletteType.Custom);

            return formatConverter;
        }

        public static void WriteErrorDetails(Wrap oculus, OVR.ovrResult result, string message)
        {
            if (result >= OVR.ovrResult.Success)
                return;

            // Retrieve the error message from the last occurring error.
            OVR.ovrErrorInfo errorInformation = oculus.GetLastError();

            string formattedMessage = string.Format("{0}. Message: {1} (Error code={2})", message, errorInformation.ErrorString, errorInformation.Result);
            Trace.WriteLine(formattedMessage);

            throw new Exception(formattedMessage);
        }

        private static void CreateGeometry(out int[] indices, out Vertex[] vertices)
        {
            int tDiv = 64;
            int yDiv = 64;
            double maxTheta = (360.0 / 180.0) * Math.PI;
            double minY = -1.0;
            double maxY = 1.0;

            double dt = maxTheta / tDiv;
            double dy = (maxY - minY) / yDiv;

            List<Vertex> listVertex = new List<Vertex>();

            //List<Vector4> listVertices = new List<Vector4>();
            //List<Vector2> listUVCoor = new List<Vector2>();
            //List<Vector3> listNormals = new List<Vector3>();
            //Vector4[] vertices = new Vector4[yDiv * tDiv];
            //MeshGeometry3D mesh = new MeshGeometry3D();

            for (int yi = 0; yi <= yDiv; yi++)
            {
                double y = minY + yi * dy;

                for (int ti = 0; ti <= tDiv; ti++)
                {
                    double t = ti * dt;

                    listVertex.Add(new Vertex
                        (GetPosition(t, y),
                        new Color4(0.5f, 0.5f, 0.5f, 1),
                        GetTextureCoordinate(t, y)));

                    //listVertices.Add(GetPosition(t, y));
                    //listNormals.Add(GetNormal(t, y));
                    //listUVCoor.Add(GetTextureCoordinate(t, y));
                }
            }

            List<int> listVerticesTriangle = new List<int>();

            for (int yi = 0; yi < yDiv; yi++)
            {
                for (int ti = 0; ti < tDiv; ti++)
                {
                    int x0 = ti;
                    int x1 = (ti + 1);
                    int y0 = yi * (tDiv + 1);
                    int y1 = (yi + 1) * (tDiv + 1);

                    listVerticesTriangle.Add(x0 + y0);
                    listVerticesTriangle.Add(x0 + y1);
                    listVerticesTriangle.Add(x1 + y0);

                    listVerticesTriangle.Add(x1 + y0);
                    listVerticesTriangle.Add(x0 + y1);
                    listVerticesTriangle.Add(x1 + y1);
                }
            }
            /*
            mesh.Freeze();
            return mesh;*/
            //Console.WriteLine(String.Join("\n", listVerticesTriangle));
            //vertices = listVertices.ToArray();
            indices = listVerticesTriangle.ToArray();
            //UVCoor = listUVCoor.ToArray();
            //normals = listNormals.ToArray();
            vertices = listVertex.ToArray();
            //return listVertices.ToArray();
        }

        internal static Vector4 GetPosition(double t, double y)
        {
            double r = Math.Sqrt(1 - y * y);
            float x = (float)(r * Math.Cos(t));
            float z = (float)(r * Math.Sin(t));

            return new Vector4(x, (float)y, z, 1.0f);
        }


        private static Vector3 GetNormal(double t, double y)
        {
            Vector4 vector4 = GetPosition(t, y);
            return new Vector3(vector4.X, vector4.Y, vector4.Z);
        }

        private static Vector2 GetTextureCoordinate(double t, double y)
        {
            System.Windows.Media.Matrix TYtoUV = new System.Windows.Media.Matrix();
            TYtoUV.Scale(1 / (2 * Math.PI), -0.5);

            System.Windows.Point p = new System.Windows.Point(t, y);
            p = p * TYtoUV;

            return new Vector2((float)p.X, (float)p.Y + 0.5f);
        }
    }

    public struct Vertex
    {
        public Vertex(Vector4 position, Color4 color, Vector2 textureUV)
        {
            Position = position;
            Color = color;
            TextureUV = textureUV;
        }

        public Vector4 Position;
        public Color4 Color;
        public Vector2 TextureUV;
    }

    public static class SharpDXHelpers
    {
        /// <summary>
        /// Convert a Vector4 to a Vector3
        /// </summary>
        /// <param name="vector4">Vector4 to convert to a Vector3.</param>
        /// <returns>Vector3 based on the X, Y and Z coordinates of the Vector4.</returns>
        public static Vector3 ToVector3(this Vector4 vector4)
        {
            return new Vector3(vector4.X, vector4.Y, vector4.Z);
        }

        /// <summary>
        /// Convert an ovrVector3f to SharpDX Vector3.
        /// </summary>
        /// <param name="ovrVector3f">ovrVector3f to convert to a SharpDX Vector3.</param>
        /// <returns>SharpDX Vector3, based on the ovrVector3f.</returns>
        public static Vector3 ToVector3(this OVR.Vector3f ovrVector3f)
        {
            return new Vector3(ovrVector3f.X, ovrVector3f.Y, ovrVector3f.Z);
        }

        /// <summary>
        /// Convert an ovrMatrix4f to a SharpDX Matrix.
        /// </summary>
        /// <param name="ovrMatrix4f">ovrMatrix4f to convert to a SharpDX Matrix.</param>
        /// <returns>SharpDX Matrix, based on the ovrMatrix4f.</returns>
        public static Matrix ToMatrix(this OculusWrap.OVR.Matrix4f ovrMatrix4f)
        {
            return new Matrix(ovrMatrix4f.M11, ovrMatrix4f.M12, ovrMatrix4f.M13, ovrMatrix4f.M14, ovrMatrix4f.M21, ovrMatrix4f.M22, ovrMatrix4f.M23, ovrMatrix4f.M24, ovrMatrix4f.M31, ovrMatrix4f.M32, ovrMatrix4f.M33, ovrMatrix4f.M34, ovrMatrix4f.M41, ovrMatrix4f.M42, ovrMatrix4f.M43, ovrMatrix4f.M44);
        }

        /// <summary>
        /// Converts an ovrQuatf to a SharpDX Quaternion.
        /// </summary>
        public static Quaternion ToQuaternion(OVR.Quaternionf ovrQuatf)
        {
            return new Quaternion(ovrQuatf.X, ovrQuatf.Y, ovrQuatf.Z, ovrQuatf.W);
        }

        /// <summary>
        /// Creates a Direct3D texture description, based on the SharpDX texture description.
        /// </summary>
        /// <param name="texture2DDescription">SharpDX texture description.</param>
        /// <returns>Direct3D texture description, based on the SharpDX texture description.</returns>
        public static OVR.D3D11.D3D11_TEXTURE2D_DESC CreateTexture2DDescription(Texture2DDescription texture2DDescription)
        {
            OVR.D3D11.D3D11_TEXTURE2D_DESC d3d11DTexture = new OVR.D3D11.D3D11_TEXTURE2D_DESC();
            d3d11DTexture.Width = (uint)texture2DDescription.Width;
            d3d11DTexture.Height = (uint)texture2DDescription.Height;
            d3d11DTexture.MipLevels = (uint)texture2DDescription.MipLevels;
            d3d11DTexture.ArraySize = (uint)texture2DDescription.ArraySize;
            d3d11DTexture.Format = (OVR.D3D11.DXGI_FORMAT)texture2DDescription.Format;
            d3d11DTexture.SampleDesc.Count = (uint)texture2DDescription.SampleDescription.Count;
            d3d11DTexture.SampleDesc.Quality = (uint)texture2DDescription.SampleDescription.Quality;
            d3d11DTexture.Usage = (OVR.D3D11.D3D11_USAGE)texture2DDescription.Usage;
            d3d11DTexture.BindFlags = (uint)texture2DDescription.BindFlags;
            d3d11DTexture.CPUAccessFlags = (uint)texture2DDescription.CpuAccessFlags;
            d3d11DTexture.MiscFlags = (uint)texture2DDescription.OptionFlags;

            return d3d11DTexture;
        }
    }

    /// <summary>
    /// Contains all the fields used by each eye.
    /// </summary>
    public class EyeTexture : IDisposable
    {
        #region IDisposable Members
        /// <summary>
        /// Dispose contained fields.
        /// </summary>
        public void Dispose()
        {
            if (SwapTextureSet != null)
            {
                SwapTextureSet.Dispose();
                SwapTextureSet = null;
            }

            if (Textures != null)
            {
                foreach (Texture2D texture in Textures)
                    texture.Dispose();

                Textures = null;
            }

            if (RenderTargetViews != null)
            {
                foreach (RenderTargetView renderTargetView in RenderTargetViews)
                    renderTargetView.Dispose();

                RenderTargetViews = null;
            }

            if (DepthBuffer != null)
            {
                DepthBuffer.Dispose();
                DepthBuffer = null;
            }

            if (DepthStencilView != null)
            {
                DepthStencilView.Dispose();
                DepthStencilView = null;
            }
        }
        #endregion

        public Texture2DDescription Texture2DDescription;
        public OculusWrap.D3D11.SwapTextureSet SwapTextureSet;
        public Texture2D[] Textures;
        public RenderTargetView[] RenderTargetViews;
        public Texture2DDescription DepthBufferDescription;
        public Texture2D DepthBuffer;
        public Viewport Viewport;
        public DepthStencilView DepthStencilView;
        public OVR.FovPort FieldOfView;
        public OVR.Sizei TextureSize;
        public OVR.Recti ViewportSize;
        public OVR.EyeRenderDesc RenderDescription;
        public OVR.Vector3f HmdToEyeViewOffset;
    }
}
