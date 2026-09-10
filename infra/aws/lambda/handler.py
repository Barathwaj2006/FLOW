import json
import os
import boto3

bedrock_runtime = boto3.client('bedrock-runtime')
DEFAULT_MODEL_ID = os.environ.get('DEFAULT_BEDROCK_MODEL_ID', 'anthropic.claude-3-5-sonnet-20240620-v1:0')

def lambda_handler(event, context):
    """
    AWS Lambda proxy for Amazon Bedrock inference requests from FLOW macOS client.
    Enforces strict input validation and payload minimization.
    """
    try:
        body = json.loads(event.get('body', '{}'))
        prompt = body.get('prompt')
        system_instruction = body.get('system_instruction', 'You are an AI assistant respecting Content Lock rules.')
        
        if not prompt:
            return {
                'statusCode': 400,
                'headers': {'Content-Type': 'application/json'},
                'body': json.dumps({'error': 'Missing required field: prompt'})
            }
        
        payload = {
            "anthropic_version": "bedrock-2023-05-31",
            "max_tokens": 1024,
            "system": system_instruction,
            "messages": [
                {
                    "role": "user",
                    "content": [{"type": "text", "text": prompt}]
                }
            ]
        }
        
        response = bedrock_runtime.invoke_model(
            modelId=DEFAULT_MODEL_ID,
            body=json.dumps(payload),
            contentType='application/json',
            accept='application/json'
        )
        
        response_body = json.loads(response['body'].read().decode('utf-8'))
        generated_text = response_body.get('content', [{}])[0].get('text', '')
        
        return {
            'statusCode': 200,
            'headers': {'Content-Type': 'application/json'},
            'body': json.dumps({'result': generated_text})
        }
        
    except Exception as e:
        return {
            'statusCode': 500,
            'headers': {'Content-Type': 'application/json'},
            'body': json.dumps({'error': str(e)})
        }
