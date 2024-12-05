import {useRef, useState} from "react";
import {useNavigate} from "react-router-dom";
import {FloatLabel} from "primereact/floatlabel";
import {InputText} from "primereact/inputtext";
import {Password} from "primereact/password";
import {Button} from "primereact/button";
import {Toast} from "primereact/toast";
import {LoginUser} from "./login";
import {Dialog} from "primereact/dialog";
import {FileUpload} from "primereact/fileupload";
import {Editor} from "primereact/editor";

export default function ShowReadLetter(letter){
    // диалоговое окно для чтения письма
    const [visibleLetterDialog, setVisibleLetterDialog] = useState(false);

    return(
        <div className="flex flex-column justify-content-center mt-5">
            <Dialog header={letter.subject}
                    visible={visibleLetterDialog}
                    style={{ width: '60vw' }}
                    onHide={() => {if (!visibleLetterDialog) return; setVisibleLetterDialog(false); }}
            >
                <div className="card">
                    <div className="flex justify-content-around"
                         style={{width: '100%', padding: '20px'}}
                    >
                        <FloatLabel style={{ margin: '5px'}}>
                            <InputText id="mailTo"
                                       value={mailTo}
                                       onChange={(e) => setMailTo(e.target.value)}
                            />
                            <label htmlFor="mailTo">Кому</label>
                        </FloatLabel>

                        <FloatLabel style={{ margin: '5px'}}>
                            <InputText id="subject"
                                       value={subject}
                                       onChange={(e) => setSubject(e.target.value)}
                            />
                            <label htmlFor="subject">Тема</label>
                        </FloatLabel>

                        <FileUpload mode="basic"
                                    name="demo[]"
                                    url="/api/upload"
                                    accept="file/*"
                                    maxFileSize={1000000}
                                    chooseLabel="Выбрать файлы"
                        />
                    </div>

                    <Editor value={mailBody}
                            onTextChange={(e) => setMailBody(e.htmlValue)}
                            style={{ height: '320px' }} />

                </div>
            </Dialog>
        </div>
    )
}