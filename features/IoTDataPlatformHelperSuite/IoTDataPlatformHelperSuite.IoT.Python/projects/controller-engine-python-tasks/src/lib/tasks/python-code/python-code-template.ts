
/** Python code template shown in the editor by default */
export const PYTHON_TEMPLATE_CONTENT: string =
    `class Code:
    def __init__(self, framework):
        self.logger = framework['logger']
        self.data_store = framework['data_store']
        self.message_bus = framework['message_bus']
        self.system = framework['system']
        self.utils = framework['utils']
        self.lbos = framework['lbos']
        # self.driver = framework['driver']

    async def main(self, inputs, outputs):
        # Add code here

        # Emit output during execution: outputs.output1.emit("something")
        # Return example: return { 'output1': inputs['input1'], 'output2': 'Hello World' }
        pass
`;
