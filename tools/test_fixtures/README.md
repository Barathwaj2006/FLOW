# Test Fixtures & Vectors

This directory contains test datasets, benchmark vectors, and audio fixtures used across unit and integration test suites.

## 1. Entity & Content Lock Test Vectors (`entities.json`)
Contains pairs of raw user statements and expected extracted entities, specifically checking negative constraints and the No-Invention Rule.

## 2. Spoken Punctuation Vectors (`punctuation.json`)
Pairs of raw transcripts with spoken punctuation words and expected formatted outputs.

## 3. Backtracking & Speech Correction Vectors (`backtracks.json`)
Phrases testing mid-sentence corrections (e.g. *"send it tomorrow actually Friday"* $\rightarrow$ *"Send it tomorrow—actually, Friday."*).

## 4. Audio Fixtures
* `silence_1000ms.wav`: Calibrated digital silence (16kHz 16-bit mono PCM) for testing VAD non-triggering.
* `speech_english_short.wav`: Baseline 3-second English sentence for latency benchmarking.
* `speech_tamil_mixed.wav`: Tamil + English code-switched utterance for multilingual evaluation.
