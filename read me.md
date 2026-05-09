# DeepScan – Real Deepfake Detector (SightEngine)

A browser‑based deepfake detection tool that uses the **SightEngine API** (free tier) to analyse images for face‑swap and GAN‑generated content.

✅ **Real API – not a fake demo**  
✅ Works entirely in your browser – no backend required  
✅ API keys stored locally (no server upload)

## 🚀 Demo

Upload an image containing a face, and get:
- Deepfake probability score (0–100%)
- Forensic metrics and key findings
- Downloadable report

## 🛠️ How to use

1. Get your free API keys from [SightEngine](https://sightengine.com) (500 checks/month)
2. Open `index.html` (or host on GitHub Pages)
3. Paste your `api_user` and `api_secret` into the form, click **Save keys**
4. Upload an image (JPG, PNG, WEBP)
5. Click **Run deepfake analysis**

## ⚠️ Limitations

- **Images only** – video deepfake detection would require a backend proxy
- SightEngine works best on face‑swap deepfakes (e.g., StyleGAN, FaceSwap)
- No detector is 100% accurate – use as one signal among many

## 📁 File structure
