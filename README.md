# 🔍 DeepScan – Real Deepfake Detector (SightEngine)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Live Demo](https://img.shields.io/badge/demo-github_pages-blue)](https://nitin-joc31.github.io/deepscan-proTOTYPE/)
[![Built with](https://img.shields.io/badge/built_with-HTML%2FCSS%2FJS-red)]()

A **browser‑based deepfake detection tool** that uses the **SightEngine API** (free tier) to analyse images for face‑swap and GAN‑generated content.

✅ **Real API – not a fake demo**  
✅ Works entirely in your browser – no backend required  
✅ API keys stored locally (`localStorage`) – no server upload  
✅ Downloadable forensic report

## 🚀 Live Demo

[Click here to try DeepScan](https://nitin-joc31.github.io/deepscan-proTOTYPE/) (you'll need your own free SightEngine API keys – see below)

## 🛠️ How to use

1. **Get your free API keys**  
   - Sign up at [SightEngine](https://sightengine.com) (free tier gives 500 image deepfake checks/month)  
   - Go to Dashboard → API credentials → copy `api_user` and `api_secret`

2. **Open the tool**  
   - Visit the [live demo](https://nitin-joc31.github.io/deepscan-proTOTYPE/) or run `index.html` locally

3. **Enter your keys**  
   - Paste `api_user` and `api_secret` into the form, click **Save keys** (keys stay in your browser)

4. **Upload an image** (JPG, PNG, WEBP) containing a face

5. **Click “Run deepfake analysis”** – get a real AI‑powered verdict

6. **Download the report** (plain text) for your records

## 📁 File structure

```text
deepscan-proTOTYPE/
├── index.html          # Main application (all code in one file)
├── LICENSE             # MIT License
└── README.md           # Project documentation (this file)
