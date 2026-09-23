---
license: apache-2.0
language:
- en
pipeline_tag: text-generation
tags:
- music
- art
---

<div align="center">
    <img src="Yi_logo.svg" width="150px" style="display: inline-block;">
    <img src="m-a-p.png" width="150px" style="display: inline-block;">
</div>

## MuPT: Symbolic Music Generative Pre-trained Transformer

MuPT is a series of pre-trained models for symbolic music generation. It was trained on a large-scale dataset of symbolic music, including millions of monophonic and polyphonic pieces from different genres and styles. The models are trained with the LLama2 architecture, and can be further used for downstream music generation tasks such as melody generation, accompaniment generation, and multi-track music generation. 

- 09/01/2024: a series of pre-trained MuPT models are released, with parameters ranging from 110M to 1.3B.

## Model architecture

The details of model architecture of MuPT-v1 are listed below:

| Name | Parameters | Training Data(Music Pieces) | Seq Length | Hidden Size | Layers | Heads |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| MuPT-v1-8192-110M | 110M | 7M x 8 epochs | 8192 | 768 | 12 | 12 |
| MuPT-v1-8192-345M | 345M | 7M x 6 epochs | 8192 | 1024 | 24 | 16 |
| MuPT-v1-8192-770M | 770M | 7M x 5 epochs | 8192 | 1280 | 36 | 20 |
| MuPT-v1-8192-1.3B | 1.3B | 7M x 8 epochs | 8192 | 1536 | 48 | 24 |

## Model Usage

#### Huggingface

##### Inference

```python
from transformers import AutoModelForCausalLM, AutoModel, AutoTokenizer

tokenizer = AutoTokenizer.from_pretrained("m-a-p/MuPT_v1_8192_110M",
                                            trust_remote_code=True,
                                            use_fast=False)
model = AutoModelForCausalLM.from_pretrained("m-a-p/MuPT_v1_8192_110mM").eval().half().cuda()

prefix = "X:1<n>L:1/8<n>Q:1/8=200<n>M:4/4<n>K:Gmin<n>|:\"Gm\" BGdB" # replace "\n" with "<n>" for all the MuPT-8192 models, but not for MuPT-4096 models
inputs = tokenizer(prefix, return_tensors="pt").to(model.device)

max_length = 256
outputs = model.generate(
    inputs.input_ids,
    max_length=max_length
)
outputs = tokenizer.decode(outputs[0])
print(outputs)
```

##### Post-processing

Since we merged multiple tracks into one track during training, we need to separate the outputs into standard ABC notation sequences. The post-processing code is as follows:

```python
import re

SEPARATORS = ['|', '|]', '||', '[|', '|:', ':|', '::']
SEP_DICT = {}
for i, sep in enumerate(SEPARATORS, start=1):
    # E.g. ' | ': ' <1>'
    SEP_DICT[' '+sep+' '] = f' <{i}>'
NEWSEP = '<|>'

def sep2tok(row):
    for sep, tok in SEP_DICT.items():
        row = row.replace(sep, tok+'<=> ')
    return row

def tok2sep(bar):
    for sep, tok in SEP_DICT.items():
        bar = bar.replace(tok, sep)
    return bar


def spacing(row):
    
    for sep in SEPARATORS:

        def subfunc(match):
            symbol = [':', '|', ']']
            if match.group(1) is None:
                return f' {sep}'
            elif match.group(1) in symbol:
                return f' {sep}{match.group(1)}'
            else:
                return ' '+sep+' '+match.group(1)
                
        pattern = r' ' + re.escape(sep) + r'(.{1})'
        row = re.sub(pattern, subfunc, row)
        row = row.replace('\n'+sep+'"', '\n '+sep+' "') # B \n|"A -> B \n | "A
        row = row.replace(' '+sep+'\n', ' '+sep+' \n')  # B |\n -> B | \n
    return row
  
 def decode(piece):
    dec_piece = ''
    idx = piece.find(' '+NEWSEP+' ')
    heads = piece[:idx]
    scores = piece[idx:]
    scores_lst = re.split(' <\|>', scores)

    all_bar_lst = []
    for bar in scores_lst:
        if bar == '':
            continue
        bar = sep2tok(bar)
        bar_lst = re.split('<=>', bar)
        bar_lst = list(map(tok2sep, bar_lst))
        if len(all_bar_lst) == 0:
            all_bar_lst = [[] for _ in range(len(bar_lst))]
        for i in range(len(bar_lst)):
            all_bar_lst[i].append(bar_lst[i])

    if len(all_bar_lst) > 1:
        # There might be the bar number like %30 at the end 
        # which need to be specially handled.
        if len(all_bar_lst[0]) > len(all_bar_lst[1]):
            last_bar_lst = all_bar_lst[0][-1].split()
            all_bar_lst[0].pop()
            for i in range(len(all_bar_lst)):
                all_bar_lst[i].append(last_bar_lst[i])
                # Add the remaining symbols to the last row.
                if i == len(all_bar_lst) - 1:
                    for j in range(i+1, len(last_bar_lst)):
                        all_bar_lst[i][-1] += ' ' + last_bar_lst[j]
        # Ensure the lengths are consistent. 
        length = len(all_bar_lst[0])
        for lst in all_bar_lst[1:]:
            # assert len(lst) == length       
            pass

    dec_piece += heads
    for i in range(len(all_bar_lst)):
        if len(all_bar_lst) > 1:
            dec_piece += f'V:{i+1}\n'
        dec_piece += ''.join(all_bar_lst[i])
        dec_piece += '\n'
    # Remove redundant spaces.
    dec_piece = re.sub(' {2,}', ' ', dec_piece)
    
    return dec_piece
```

Processed Output:
```shell
X:1
L:1/8
Q:1/8=200
M:4/4<n>K:Gmin
|:\"Gm\" BGdB fdBG |\"F\" AFcF dFcF |\"Gm\" BGdG gFBF |\"F\" AFAG AF F2 |\"Gm\" BGBd fffd |\"F\" cdcB cdeg |
\"Gm\" fdcB\"Eb\" AFcA |1 BGFG\"F\" AFGc :|2 BGFG\"F\" AF F2 ||
```

Once you encode the post-processed ABC notation into audio, you will hear the following music.

<audio controls src="https://cdn-uploads.huggingface.co/production/uploads/640701cb4dc5f2846c91d4eb/gnBULaFjcUyXYzzIwXLZq.mpga"></audio>

#### Megatron-LM

We now the provide usage based on [Megatron-LM](https://github.com/NVIDIA/Megatron-LM/tree/main).

Before starting, make sure you have setup the relevant environment and codebase. 
 
```shell
# pull Megatron-LM codebase
mkdir -p /path/to/workspace && cd /path/to/workspace
git clone https://github.com/NVIDIA/Megatron-LM.git
# download the pre-trained MuPT models checkpoint and vocab files from Huggingface page
mkdir -p /models/MuPT_v0_8192_1.3B && cd /models/MuPT_v0_8192_1.3B
wget -O model_optim_rng.pt https://huggingface.co/m-a-p/MuPT_v0_8192_1.3B/resolve/main/model_optim_rng.pt?download=true
wget -O newline.vocab https://huggingface.co/m-a-p/MuPT_v0_8192_1.3B/resolve/main/newline.vocab?download=true
wget -O newline.txt https://huggingface.co/m-a-p/MuPT_v0_8192_1.3B/resolve/main/newline.txt?download=true
```

We recommend using the latest version of [NGC's PyTorch container](https://catalog.ngc.nvidia.com/orgs/nvidia/containers/pytorch) for MuPT inference. See more details in [Megatron-LM](https://github.com/NVIDIA/Megatron-LM/tree/main)

```shell
# pull the latest NGC's PyTorch container, mount the workspace directory and enter the container
docker run --gpus all -it --name megatron --shm-size=16g -v $PWD:/workspace -p 5000:5000 nvcr.io/nvidia/pytorch:23.11-py3 /bin/bash
```

Once you enter the container, you can start a REST server for inference. 

<details>
    <summary>Click to expand the example script</summary>

    #!/bin/bash
    # This example will start serving the 1.3B model.
    export CUDA_DEVICE_MAX_CONNECTIONS=1

    DISTRIBUTED_ARGS="--nproc_per_node 1 \
                    --nnodes 1 \
                    --node_rank 0 \
                    --master_addr localhost \
                    --master_port 6000"

    CHECKPOINT=/path/to/model/checkpoint/folder
    VOCAB_FILE=/path/to/vocab/file
    MERGE_FILE=/path/to/merge/file

    MODEL_SIZE="1.3B"
    if   [[ ${MODEL_SIZE} == "110M" ]];   then HIDDEN_SIZE=768;  NUM_HEAD=12; NUM_QUERY_GROUP=12; NUM_LAYERS=12; FFN_HIDDEN_SIZE=3072; NORM_EPS=1e-5;
    elif [[ ${MODEL_SIZE} == "345M" ]];   then HIDDEN_SIZE=1024;  NUM_HEAD=16; NUM_QUERY_GROUP=16; NUM_LAYERS=24; FFN_HIDDEN_SIZE=4096; NORM_EPS=1e-5;
    elif [[ ${MODEL_SIZE} == "770M" ]];   then HIDDEN_SIZE=1280;  NUM_HEAD=20; NUM_QUERY_GROUP=20; NUM_LAYERS=36; FFN_HIDDEN_SIZE=5120; NORM_EPS=1e-5;
    elif [[ ${MODEL_SIZE} == "1.3B" ]];   then HIDDEN_SIZE=1536;  NUM_HEAD=24; NUM_QUERY_GROUP=24; NUM_LAYERS=48; FFN_HIDDEN_SIZE=6144; NORM_EPS=1e-5;
    else echo "invalid MODEL_SIZE: ${MODEL_SIZE}"; exit 1
    fi
    MAX_SEQ_LEN=8192
    MAX_POSITION_EMBEDDINGS=8192

    pip install flask-restful

    torchrun $DISTRIBUTED_ARGS tools/run_text_generation_server.py   \
        --tensor-model-parallel-size 1  \
        --pipeline-model-parallel-size 1  \
        --num-layers ${NUM_LAYERS}  \
        --hidden-size ${HIDDEN_SIZE}  \
        --ffn-hidden-size ${FFN_HIDDEN_SIZE} \
        --load ${CHECKPOINT}  \
        --group-query-attention \
        --num-query-groups ${NUM_QUERY_GROUP} \
        --position-embedding-type rope \
        --num-attention-heads ${NUM_HEAD}  \
        --max-position-embeddings ${MAX_POSITION_EMBEDDINGS}  \
        --tokenizer-type GPT2BPETokenizer  \
        --normalization RMSNorm \
        --norm-epsilon ${NORM_EPS} \
        --make-vocab-size-divisible-by 1 \
        --swiglu \
        --use-flash-attn \
        --bf16  \
        --micro-batch-size 1  \
        --disable-bias-linear \
        --no-bias-gelu-fusion \
        --untie-embeddings-and-output-weights \
        --seq-length ${MAX_SEQ_LEN}  \
        --vocab-file $VOCAB_FILE  \
        --merge-file $MERGE_FILE  \
        --attention-dropout 0.0 \
        --hidden-dropout 0.0 \
        --weight-decay 1e-1 \
        --clip-grad 1.0 \
        --adam-beta1 0.9 \
        --adam-beta2 0.95 \
        --adam-eps 1e-8 \
        --seed 42

</details>


Use CURL to query the server directly, note that the newline token `\n` is represented by `<n>` in the vocabulary, so we need to replace the newline token with `<n>` in both the prompt and the generated tokens. 

```shell
curl 'http://localhost:6000/api' -X 'PUT' -H 'Content-Type: application/json; charset=UTF-8'  -d '{"prompts":["X:1<n>L:1/8<n>Q:1/8=200<n>M:4/4<n>K:Gmin<n>|:\"Gm\" BGdB"], "tokens_to_generate":4096}'
```
Processed Output:
```shell
X:1
L:1/8
Q:1/8=200
M:4/4<n>K:Gmin
|:\"Gm\" BGdB fdBG |\"F\" AFcF dFcF |\"Gm\" BGdG gFBF |\"F\" AFAG AF F2 |\"Gm\" BGBd fffd |\"F\" cdcB cdeg |
\"Gm\" fdcB\"Eb\" AFcA |1 BGFG\"F\" AFGc :|2 BGFG\"F\" AF F2 ||
```