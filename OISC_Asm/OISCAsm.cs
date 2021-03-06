﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OISC_Compiler.Instructions;

namespace OISC_Compiler
{
    public interface IAssembler
    {
        byte[] Assemble();
    }

    public class OISCAsm :IAssembler
    {
        private String[] _sourceCodeLines;

        public OISCAsm(String[] sourceCodeLines)
        {
            _sourceCodeLines = sourceCodeLines;
        }

        public byte[] Assemble()
        {
            ICollection<AddressableInstruction> sourceTree = ParseSource();
            
            AddressableInstruction lastInstruction = sourceTree.Last();
            long binarySize = lastInstruction.Address.BinaryAddress + lastInstruction.BinaryLength;

            // Create an array of the required size to hold the binary data.
            // Size is taken from the final binary address used.
            byte[] binary = new byte[binarySize];

            // Assemble the binary for each instruction and store it in the array.
            foreach (AddressableInstruction instruction in sourceTree)
            {
                byte[] instructionBinary = instruction.AssembleBinary();
                Array.Copy(instructionBinary, 0, binary, instruction.Address.BinaryAddress, instruction.BinaryLength);
            }

            return binary;
        }

        private ICollection<AddressableInstruction> ParseSource()
        {
            InstructionFactory instructionParser = new InstructionFactory();

            // We parse the source and build a list of all source 
            // instructions (including comments), and a dictionary
            // of executable instructions, indexed by their source
            // starting address.
            List<Instruction> sourceList = new List<Instruction>();
            Dictionary<int, AddressableInstruction> instructionDictionary = new Dictionary<int, AddressableInstruction>();
            Dictionary<String, AddressableInstruction> labeledInstructionDictionary = new Dictionary<string, AddressableInstruction>();
            int instructionSourceAddress = 0;
            int instructionSourceLineNumber = 0;

            // Loop through each line of source code and create an instruction for it.
            foreach (String sourceLine in _sourceCodeLines)
            {
                Instruction sourceInstruction = instructionParser.GenerateInstruction(sourceLine, instructionSourceLineNumber, instructionSourceAddress);

                if (sourceInstruction != null)
                {
                    AddressableInstruction addressableInstruction = sourceInstruction as AddressableInstruction;
                    if (addressableInstruction != null)
                    {
                        instructionDictionary.Add(instructionSourceAddress, addressableInstruction);

                        // If the instruction has a label, store a mapping so we can resolve labeled branches later.
                        if (!String.IsNullOrEmpty(addressableInstruction.Address.AddressLabel))
                        {
                            labeledInstructionDictionary.Add(addressableInstruction.Address.AddressLabel, addressableInstruction);
                        }

                        instructionSourceAddress += addressableInstruction.SourceLength;
                    }

                    sourceList.Add(sourceInstruction);

                    instructionSourceLineNumber++;
                }
            }

            // Loop through each instruction and map each branch address to the destination instruction.
            // This is done at the source level at this stage so we have a correctly mapped instruction 
            // tree before we start generating binary and create the actual addresses.
            foreach (var instruction in instructionDictionary)
            {
                IBranchingInstruction branchingInstruction = instruction.Value as IBranchingInstruction;
                if (branchingInstruction != null)
                {
                    branchingInstruction.MapBranchAddress(instructionDictionary, labeledInstructionDictionary);
                }

                IAddressedOperands addressedOperandsInstruction = instruction.Value as IAddressedOperands;
                if (addressedOperandsInstruction != null)
                {
                    addressedOperandsInstruction.MapAddressedOperands(instructionDictionary, labeledInstructionDictionary);
                }

                AddressableMemoryInstruction memoryValue = instruction.Value as AddressableMemoryInstruction;
                if (memoryValue != null)
                {
                    memoryValue.MapMemoryValue(instructionDictionary, labeledInstructionDictionary);
                }
            }

            return instructionDictionary.Values;
        }


    }
}
