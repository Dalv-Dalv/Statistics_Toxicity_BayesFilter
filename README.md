# Toxicity Bayes Filter

### A Naive Bayes Filter for Detecting Toxic Messages in C#

This project implements a **multi-label Naive Bayes classifier** for message toxicity detection.
It supports **multiple toxicity categories**, **neutral classification**, **token bigrams**, **word frequency analysis**, and **test-dataset evaluation**.

---

## Features

* **Multi-category toxicity detection**
  (model can predict zero, one, or multiple toxic labels)

* **Configurable tokenization**
  * Single words
  * Optional **bigrams** (`"word1 word2"`)

* **Laplace smoothing** for unseen words

* **Automatic dataset cleaning & preprocessing**

* **Rare-word filtering** to reduce noise

* **Optional useless-word filtering** (based on probability variance)

* **Debug mode** to print per-word likelihood computations

* **Full test-suite with metrics:**
  * Accuracy
  * Precision
  * Recall
  * F1 score
  * TP / TN / FP / FN counts

---

## Usage
Run the program, answer the prompts and it will automatically run the test dataset and show accuracy metrics. Afterwards you can input custom messages to test. Putting `#` at the start of the message will make it show a detailed breakdown of the labeling process.
