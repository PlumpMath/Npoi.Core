﻿using ICSharpCode.SharpZipLib.Zip;
using Npoi.Core.Util;
using System;
using System.IO;
using System.Xml.Linq;

namespace Npoi.Core.OpenXml4Net.OPC.Internal.Marshallers
{
    /**
     * Zip part marshaller. This marshaller is use to save any part in a zip stream.
     *
     * @author Julien Chable
     */

    public class ZipPartMarshaller : PartMarshaller
    {
        private static POILogger logger = POILogFactory.GetLogger(typeof(ZipPartMarshaller));

        /**
         * Save the specified part.
         *
         * @throws OpenXml4NetException
         *             Throws if an internal exception is thrown.
         */

        public bool Marshall(PackagePart part, Stream os)
        {
            if (!(os is ZipOutputStream))
            {
                logger.Log(POILogger.ERROR, "Unexpected class " + os.GetType().Name);
                throw new OpenXml4NetException("ZipOutputStream expected !");
                // Normally should happen only in developement phase, so just throw
                // exception
            }

            ZipOutputStream zos = (ZipOutputStream)os;
            string name = ZipHelper
                    .GetZipItemNameFromOPCName(part.PartName.URI
                            .OriginalString);
            ZipEntry partEntry = new ZipEntry(name);
            try
            {
                // Create next zip entry
                zos.PutNextEntry(partEntry);

                // Saving data in the ZIP file
                Stream ins = part.GetInputStream();
                byte[] buff = new byte[ZipHelper.READ_WRITE_FILE_BUFFER_SIZE];
                int totalRead = 0;
                while (true)
                {
                    int resultRead = ins.Read(buff, 0, buff.Length);
                    if (resultRead == 0)
                    {
                        // End of file reached
                        break;
                    }
                    zos.Write(buff, 0, resultRead);
                    totalRead += resultRead;
                }
                zos.CloseEntry();
            }
            catch (IOException ioe)
            {
                logger.Log(POILogger.ERROR, "Cannot write: " + part.PartName + ": in ZIP", ioe);
                return false;
            }

            // Saving relationship part
            if (part.HasRelationships)
            {
                PackagePartName relationshipPartName = PackagingUriHelper
                        .GetRelationshipPartName(part.PartName);

                MarshallRelationshipPart(part.Relationships,
                        relationshipPartName, zos);
            }
            return true;
        }

        /**
         * Save relationships into the part.
         *
         * @param rels
         *            The relationships collection to marshall.
         * @param relPartName
         *            Part name of the relationship part to marshall.
         * @param zos
         *            Zip output stream in which to save the XML content of the
         *            relationships serialization.
         */

        public static bool MarshallRelationshipPart(
                PackageRelationshipCollection rels, PackagePartName relPartName,
                ZipOutputStream zos)
        {
            // Building xml
            var xmlOutDoc = new XDocument();
            // make something like <Relationships
            // xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
            System.Xml.XmlNamespaceManager xmlnsManager = null;
            using (var reader = xmlOutDoc.CreateReader())
            {
                xmlnsManager = new System.Xml.XmlNamespaceManager(reader.NameTable);
            }
            xmlnsManager.AddNamespace("x", PackageNamespaces.RELATIONSHIPS);

            XNamespace ns = PackageNamespaces.RELATIONSHIPS;

            var root = new XElement(ns + PackageRelationship.RELATIONSHIPS_TAG_NAME);

            xmlOutDoc.Add(root);

            // <Relationship
            // TargetMode="External"
            // Id="rIdx"
            // Target="http://www.custom.com/images/pic1.jpg"
            // Type="http://www.custom.com/external-resource"/>

            Uri sourcePartURI = PackagingUriHelper
                    .GetSourcePartUriFromRelationshipPartUri(relPartName.URI);

            foreach (PackageRelationship rel in rels)
            {
                // the relationship element
                XElement relElem = new XElement(ns + PackageRelationship.RELATIONSHIP_TAG_NAME);

                // the relationship ID
                relElem.SetAttributeValue(PackageRelationship.ID_ATTRIBUTE_NAME, rel.Id);

                // the relationship Type
                relElem.SetAttributeValue(PackageRelationship.TYPE_ATTRIBUTE_NAME, rel
                        .RelationshipType);

                // the relationship Target
                String targetValue;
                Uri uri = rel.TargetUri;
                if (rel.TargetMode == TargetMode.External)
                {
                    // Save the target as-is - we don't need to validate it,
                    //  alter it etc
                    targetValue = uri.OriginalString;

                    // add TargetMode attribute (as it is external link external)
                    relElem.SetAttributeValue(PackageRelationship.TARGET_MODE_ATTRIBUTE_NAME,
                            "External");
                }
                else
                {
                    targetValue = PackagingUriHelper.RelativizeUri(
                            sourcePartURI, rel.TargetUri, true).ToString();
                }
                relElem.SetAttributeValue(PackageRelationship.TARGET_ATTRIBUTE_NAME,
                        targetValue);
                root.AppendChild(relElem);
            }

            //xmlOutDoc.Normalize();

            // String schemaFilename = Configuration.getPathForXmlSchema()+
            // File.separator + "opc-relationships.xsd";

            // Save part in zip
            ZipEntry ctEntry = new ZipEntry(ZipHelper.GetZipURIFromOPCName(
                    relPartName.URI.ToString()).OriginalString);
            try
            {
                zos.PutNextEntry(ctEntry);

                StreamHelper.SaveXmlInStream(xmlOutDoc, zos);
                zos.CloseEntry();
            }
            catch (IOException e)
            {
                logger.Log(POILogger.ERROR, "Cannot create zip entry " + relPartName, e);
                return false;
            }
            return true; // success
        }
    }
}