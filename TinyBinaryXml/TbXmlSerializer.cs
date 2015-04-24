using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text;

namespace TinyBinaryXml
{
	public class TbXmlSerializer
	{
		private List<TbXmlNodeTemplate> nodeTemplates = new List<TbXmlNodeTemplate>();

		private List<TbXmlNode> nodes = new List<TbXmlNode>();

		private ushort nodeIdInc = 0;

		private ushort nodeTemplateIdInc = 0;

		public byte[] SerializeXmlString(string xmlString)
		{
			if(string.IsNullOrEmpty(xmlString))
			{
				return null;
			}

			nodeIdInc = 0;
			nodeTemplateIdInc = 0;

			XmlDocument doc = new XmlDocument();
			doc.LoadXml(xmlString);

			TbXmlNode docNode = new TbXmlNode();
			docNode.childrenIds = new List<ushort>();

			XmlNodeList xmlNodeList = doc.ChildNodes;
			foreach(XmlNode xmlNode in xmlNodeList)
			{
				if(xmlNode.NodeType == XmlNodeType.Element)
				{
					ProcessXmlNode(docNode, xmlNode);
				}
			}

			BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream(), Encoding.UTF8);
			Serialize(binaryWriter);

			byte[] buffer = new byte[binaryWriter.BaseStream.Length];
			binaryWriter.BaseStream.Position = 0;
			binaryWriter.BaseStream.Read(buffer, 0, (int)binaryWriter.BaseStream.Length);
			binaryWriter.Close();
//			binaryWriter.Dispose();

			return buffer;
		}

		private void ProcessXmlNode(TbXmlNode parentNode, XmlNode xmlNode)
		{
			TbXmlNodeTemplate nodeTemplate = GetNodeTemplate(xmlNode);
			if(nodeTemplate == null)
			{
				nodeTemplate = new TbXmlNodeTemplate();
				nodeTemplates.Add(nodeTemplate);
				nodeTemplate.attributeNames = new List<string>();
				nodeTemplate.attributeTypes = new List<TB_XML_ATTRIBUTE_TYPE>();
				nodeTemplate.id = nodeTemplateIdInc++;
				nodeTemplate.name = xmlNode.Name;
				foreach(XmlAttribute xmlAttribute in xmlNode.Attributes)
				{
					string attributeString = xmlAttribute.Value;
					float attributeFloat;
					if(float.TryParse(attributeString, out attributeFloat))
					{
						nodeTemplate.attributeTypes.Add(TB_XML_ATTRIBUTE_TYPE.DOUBLE);
					}
					else
					{
						nodeTemplate.attributeTypes.Add(TB_XML_ATTRIBUTE_TYPE.STRING);
					}
					nodeTemplate.attributeNames.Add(xmlAttribute.Name);
				}
			}

			TbXmlNode node = new TbXmlNode();
			nodes.Add(node);
			node.attributeValues = new List<object>();
			node.childrenIds = new List<ushort>();
			node.id = nodeIdInc++;
			node.templateId = nodeTemplate.id;
			parentNode.childrenIds.Add(node.id);
			foreach(XmlAttribute xmlAttribute in xmlNode.Attributes)
			{
				string attributeString = xmlAttribute.Value;
                double attributeFloat;
				if(double.TryParse(attributeString, out attributeFloat))
				{
					node.attributeValues.Add(attributeFloat);
				}
				else
				{
					node.attributeValues.Add(attributeString);
				}
			}

			foreach(XmlNode subXmlNode in xmlNode.ChildNodes)
			{
				if(subXmlNode.NodeType == XmlNodeType.Element)
				{
					ProcessXmlNode(node, subXmlNode);
				}
				else if(subXmlNode.NodeType == XmlNodeType.Text || subXmlNode.NodeType == XmlNodeType.CDATA)
				{
					if(node.text == null)
					{
						node.text = subXmlNode.Value;
					}
					else
					{
						node.text += subXmlNode.Value;
					}
				}
			}
		}

		private TbXmlNodeTemplate GetNodeTemplate(ushort templateId)
		{
			foreach(TbXmlNodeTemplate nodeTemplate in nodeTemplates)
			{
				if(nodeTemplate.id == templateId)
				{
					return nodeTemplate;
				}
			}
			return null;
		}

		private TbXmlNodeTemplate GetNodeTemplate(XmlNode xmlNode)
		{
			foreach(TbXmlNodeTemplate nodeTemplate in nodeTemplates)
			{
				if(XmlNodeMatchTemplate(xmlNode, nodeTemplate))
				{
					return nodeTemplate;
				}
			}
			return null;
		}

		private bool XmlNodeMatchTemplate(XmlNode xmlNode, TbXmlNodeTemplate nodeTemplate)
		{
			if(nodeTemplate.name != xmlNode.Name)
			{
				return false;
			}

            XmlAttributeCollection xmlAttributes = xmlNode.Attributes;
            int numAttributes = xmlAttributes == null ? 0 : xmlAttributes.Count;
            for (int i = 0; i < numAttributes; ++i)
            {
                XmlAttribute xmlAttribute = xmlAttributes[i];
                if (nodeTemplate.attributeNames != null && !nodeTemplate.attributeNames[i].Equals(xmlAttribute.Name))
                {
                    return false;
                }

                double attributeFloat;
                bool isAttributeFloat = double.TryParse(xmlAttribute.Value, out attributeFloat);
                if ((isAttributeFloat && nodeTemplate.attributeTypes[i] != TB_XML_ATTRIBUTE_TYPE.DOUBLE) || 
                    (!isAttributeFloat && nodeTemplate.attributeTypes[i] == TB_XML_ATTRIBUTE_TYPE.DOUBLE))
                {
                    return false;
                }
            }
			return xmlNode.Attributes.Count == nodeTemplate.attributeNames.Count;
		}

		private void Serialize(BinaryWriter binaryWriter)
		{
			binaryWriter.Write((ushort)nodeTemplates.Count);
			foreach(TbXmlNodeTemplate nodeTemplate in nodeTemplates)
			{
				SerializeNodeTemplate(binaryWriter, nodeTemplate);
			}

			binaryWriter.Write((ushort)nodes.Count);
			foreach(TbXmlNode node in nodes)
			{
				SerializeNode(binaryWriter, node);
			}
		}

		private void SerializeNodeTemplate(BinaryWriter binaryWriter, TbXmlNodeTemplate nodeTemplate)
		{
			binaryWriter.Write(nodeTemplate.id);

			binaryWriter.Write(nodeTemplate.name);

			binaryWriter.Write((ushort)nodeTemplate.attributeNames.Count);
			foreach(string attributeName in nodeTemplate.attributeNames)
			{
				binaryWriter.Write(attributeName);
			}

			foreach(TB_XML_ATTRIBUTE_TYPE attributeType in nodeTemplate.attributeTypes)
			{
				binaryWriter.Write((byte)attributeType);
			}
		}

		private void SerializeNode(BinaryWriter binaryWriter, TbXmlNode node)
		{
			TbXmlNodeTemplate nodeTemplate = GetNodeTemplate(node.templateId);

			binaryWriter.Write(node.id);

			binaryWriter.Write(node.templateId);

			binaryWriter.Write((ushort)node.childrenIds.Count);
			foreach(ushort childId in node.childrenIds)
			{
				binaryWriter.Write(childId);
			}
			
			int attributeIndex = 0;
			foreach(object attributeValue in node.attributeValues)
			{
				if(nodeTemplate.attributeTypes[attributeIndex] == TB_XML_ATTRIBUTE_TYPE.DOUBLE)
				{
                    binaryWriter.Write((double)attributeValue);
				}
				else
				{
					binaryWriter.Write((string)attributeValue);
				}
				++attributeIndex;
			}

			if(string.IsNullOrEmpty(node.text))
			{
				binaryWriter.Write((byte)0);
			}
			else
			{
				binaryWriter.Write((byte)1);
				binaryWriter.Write(node.text);
			}
		}
	}
}

