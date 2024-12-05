import {useNavigate} from "react-router-dom";
import { Menubar } from 'primereact/menubar';
import {useEffect, useRef, useState} from "react";
import { DataTable } from 'primereact/datatable';
import { Column } from 'primereact/column';
import {isNullOrUndef} from "chart.js/helpers";
import {Toast} from "primereact/toast";
import { Dialog } from 'primereact/dialog';
import { Button } from 'primereact/button';
import { InputText } from 'primereact/inputtext';
import { FloatLabel } from "primereact/floatlabel";
import { Editor } from "primereact/editor";
import { FileUpload } from 'primereact/fileupload';
import { ConfirmDialog } from 'primereact/confirmdialog';
import { ProgressSpinner } from 'primereact/progressspinner';

export default function ShowHome(){
    // диалоговое окно для отправки письма
    const [visibleLetterDialog, setVisibleLetterDialog] = useState(false);

    // диалоговое окно для отправки запроса дружбы (обмен ключами)
    const [visibleFriendDialog, setVisibleFriendDialog] = useState(false);

    // диалоговое окно для ответа на запрос дружбы
    const [visibleFriendCoinf, setVisibleFriendCoinf] = useState(false);

    // показываем пользователю, что письма загружаются (выводим ProgressSpinner)
    const [showSkeleton, setShowSkeleton] = useState(true);

    // для уведомлений Toast
    const toast = useRef(null);

    // письма из ящика
    const [letters, setLetters] = useState([]);

    // выбранное письмо
    const [selectedLetter, setSelectedLetter] = useState(null);

    // получатель письма
    const [mailTo, setMailTo] = useState('');

    // тема письма
    const [subject, setSubject] = useState('');

    // текст письма
    const [mailBody, setMailBody] = useState('');

    // адрес на который будет отправлен запрос дружбы
    const [friend, setFriend] = useState('');

    // отправленный запрос дружбы
    const [friendSender, setFriendSender] = useState(null);

    // текущая открытая папка
    const [curFolder, setCurFolder] = useState('Input');

    // сообщение окна подтверждения
    const [coinfMessage, setCoinfMessage] = useState('');

    const navigate = useNavigate();

    // попытка отправки запроса дружбы
    async function sendFriendRequest(from) {
        try {
            const response = await fetch("http://localhost:5113/friends/SendFriendRequest", {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    EmailFrom: from,
                    EmailTo: friend
                }),
            });

            if (!response.ok) {
                if (response.status === 400) {
                    const errorData = await response.text();
                    const errorMessage = errorData || 'Неизвестная ошибка при отправке запроса';
                    throw new Error(errorMessage);
                } else {
                    const text = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}, message: ${text}`);
                }
            }

            const data = await response.text();
            toast.current.show({
                severity: 'success',
                summary: 'Запрос отправлен',
                detail: "Запрос дружбы отправлен пользователю"
            });
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: error.message
            });
        }
    }

    // футер для диалогового окна отправки запроса дружбы
    const footerFriendContent = (
        <div>
            <Button label="Отправить"
                    icon="pi pi-check"
                    onClick={() => {
                        setVisibleFriendDialog(false);

                        const credentialsJSON = localStorage.getItem('userCredentials');

                        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

                        const index = parseInt(localStorage.getItem('curUser'));

                        sendFriendRequest(data[index]?.email);
                    }}
            />
        </div>
    );

    // футер диалогового окна отправки письма
    const footerLetterContent = (
        <div>
            <Button label="Отправить"
                    icon="pi pi-check"
                    onClick={() => {
                        setVisibleLetterDialog(false);

                        const credentialsJSON = localStorage.getItem('userCredentials');

                        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

                        const index = parseInt(localStorage.getItem('curUser'));

                        fetch("http://localhost:5113/mail/SendMail", {
                            method: "POST",
                            headers: {
                                "Content-Type": "application/json", // Тип содержимого
                            },
                            body: JSON.stringify({
                                Email: data[index]?.email,
                                AppPassword: data[index]?.password,
                                RecipientEmail: mailTo,
                                Subject: subject,
                                Body: mailBody
                            }),
                        })
                            .then(response => {
                                // Обработка ответа от сервера
                                if (!response.ok) {
                                    // Проверка на ошибки HTTP (4xx или 5xx)
                                    // Создаём ошибку, если ответ не успешный.
                                    throw new Error(`HTTP error! status: ${response.status}`);
                                }
                                return response;
                            })
                            .then(data => {
                                toast.current.show({
                                    severity: 'success',
                                    summary: 'Письмо отправлен',
                                    detail: `Письмо отправлено пользователю ${mailTo}`
                                });
                            })
                            .catch(error => {
                                // Обработка ошибок
                                console.error("Ошибка:", error);
                                toast.current.show({
                                    severity: 'error',
                                    summary: 'Ошибка',
                                    detail: 'Что-то пошло не так :('
                                });
                            });
                    }}
            />
        </div>
    );

    const checkUser = () => {
        if(isNullOrUndef(localStorage.getItem('userCredentials'))) {
            navigate('/login');
        }
    }

    // если пользователь не зашёл в аккаунт заставляем его зайти
    useEffect(() => {
        checkUser();
    }, []);

    // получаем письма из ящика с указанной папки
    const getLettersByCommand = async (folder) => {
        const credentialsJSON = localStorage.getItem('userCredentials');
        const data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        const index = parseInt(localStorage.getItem('curUser'));

        const curUser = data[index];

        setShowSkeleton(true);

        try {
            const response = await fetch("http://localhost:5113/mail/GetMails", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    Email: curUser.email,
                    AppPassword: curUser.password,
                    Action: folder,
                }),
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const data = await response.json();
            setLetters(data);
            setCurFolder(folder);
            setShowSkeleton(false);
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: 'Что-то пошло не так :('
            });
        }
    };

    const items = [
        {
            label: 'Написать письмо',
            icon: 'pi pi-envelope',
            command: () => {
                setVisibleLetterDialog(true);
            }
        },
        {
            label: 'Входящие',
            icon: 'pi pi-inbox',
            command: () => {
                getLettersByCommand('Input');
            }
        },
        {
            label: 'Отправленные',
            icon: 'pi pi-send',
            command: () => {
                getLettersByCommand('Output');
            }
        },
        {
            label: 'Черновики',
            icon: 'pi pi-book',
            command: () => {
                getLettersByCommand('Drafts');
            }
        },
        {
            label: 'Корзина',
            icon: 'pi pi-trash',
            command: () => {
                getLettersByCommand('Trash');
            }
        },
        {
            label: 'Отправить запрос дружбы',
            icon: 'pi pi-user-plus',
            command: () => {
                setVisibleFriendDialog(true);
            }
        },
    ];

    useEffect(() => {
        !isNullOrUndef(localStorage.getItem('userCredentials')) && getLettersByCommand('Input');
    }, []);

    useEffect(() =>{
        // проверить есть ли для пользователя запросы дружбы
        if(!isNullOrUndef(localStorage.getItem('userCredentials'))) {
            const credentialsJSON = localStorage.getItem('userCredentials');

            let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            const index = parseInt(localStorage.getItem('curUser'));

            checkRequest(data[index]?.email);
        }
    },[]);

    // проверка запросов на дружбу для пользователя
    const checkRequest = async (user) => {
        try {
            const response = await fetch("http://localhost:5113/friends/FindRequestForUser", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(user),
            });

            if (!response.ok) {
                if (response.status === 400) {
                    const errorData = await response.text();
                    const errorMessage = errorData || 'Неизвестная ошибка при отправке запроса';
                    throw new Error(errorMessage);
                } else {
                    const text = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}, message: ${text}`);
                }
            }
            const data = await response.json();

            console.log(data);
            if(typeof data !== 'string'){
                setFriendSender(data);

                console.log(friendSender);
            }
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: error.message
            });
        }
    }

    useEffect(() => {
        if(!isNullOrUndef(friendSender)) {
            setCoinfMessage(`Пользователь ${friendSender.userFrom} отправил вам запрос дружбы. Принять?`);
            setVisibleFriendCoinf(true);
        }
    }, [friendSender]);

    // отправка на сервер ответа пользователя на запрос
    const answerOnFriendRequest = async (answer) => {
        try {
            const credentialsJSON = localStorage.getItem('userCredentials');

            let users = credentialsJSON ? JSON.parse(credentialsJSON) : [];

            const index = parseInt(localStorage.getItem('curUser'));

            const response = await fetch("http://localhost:5113/friends/AnswerFriendRequest", {
                method: "DELETE",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify({
                    RequestId: friendSender.id,
                    EmailFrom: friendSender.userFrom,
                    EmailTo: users[index]?.email,
                    Answer: answer
                }),
            });

            if (!response.ok) {
                if (response.status === 400) {
                    const errorData = await response.text();
                    const errorMessage = errorData || 'Неизвестная ошибка при отправке запроса';
                    throw new Error(errorMessage);
                } else {
                    const text = await response.text();
                    throw new Error(`HTTP error! status: ${response.status}, message: ${text}`);
                }
            }
            const data = await response.text();

            // Перезагружаем страницу т.к. для пользователя может быть не 1 запрос
            window.location.reload();
        } catch (error) {
            console.error("Ошибка:", error);
            toast.current.show({
                severity: 'error',
                summary: 'Ошибка',
                detail: error.message
            });
        }
    }

    const showSkeletonOrData = () => {
        if (showSkeleton) {
            return(
                <div className="flex align-items-center justify-content-center"
                    style={{width:"100%", height:"75vh"}}>
                    <ProgressSpinner />
                </div>
                );
        }
        else{
            return (
                <DataTable value={letters}
                           tableStyle={{minWidth: '60rem'}}
                           style={{width: '100%'}}
                           emptyMessage={"Папка пуста :("}
                           stripedRows
                           selectionMode="single"
                           selection={selectedLetter}
                           onSelectionChange={(e) => setSelectedLetter(e.value)} dataKey="id"
                           onRowSelect={onRowSelect}
                >
                    <Column field="from" header="Отправитель"></Column>
                    <Column field="subject" header="Тема"></Column>
                    <Column field="date" header="Дата"></Column>
                </DataTable>);
        }
    }

    const onRowSelect = (event) => {
        const credentialsJSON = localStorage.getItem('userCredentials');

        let data = credentialsJSON ? JSON.parse(credentialsJSON) : [];

        const index = parseInt(localStorage.getItem('curUser'));

        fetch("http://localhost:5113/mail/GetLetter", {
            method: "POST",
            headers: {
                "Content-Type": "application/json", // Тип содержимого
            },
            body: JSON.stringify({
                Email: data[index]?.email,
                AppPassword: data[index]?.password,
                Action: curFolder,
                LetterId: event.data.id
            }),
        })
            .then(response => {
                // Обработка ответа от сервера
                if (!response.ok) {
                    // Проверка на ошибки HTTP (4xx или 5xx)
                    // Создаём ошибку, если ответ не успешный.
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                return response;
            })
            .then(data => {
                toast.current.show({
                    severity: 'success',
                    summary: 'Письмо отправлен',
                    detail: `Письмо отправлено пользователю ${mailTo}`
                });
            })
            .catch(error => {
                // Обработка ошибок
                console.error("Ошибка:", error);
                toast.current.show({
                    severity: 'error',
                    summary: 'Ошибка',
                    detail: 'Что-то пошло не так :('
                });
            });

        toast.current.show({
            severity: 'info',
            summary: 'Product Selected',
            detail: `Name: ${event.data.id}`,
            life: 3000
        });
    };

    return(
        <div>
            <Menubar model={items}
                     style={{width: '100%'}}
            />

            {showSkeletonOrData()}


            <Dialog header="Написать письмо"
                    visible={visibleLetterDialog}
                    style={{ width: '60vw' }}
                    onHide={() => {if (!visibleLetterDialog) return; setVisibleLetterDialog(false); }}
                    footer={footerLetterContent}
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


            <Dialog header="Отправить запрос дружбы"
                    visible={visibleFriendDialog}
                    style={{ width: '30vw' }}
                    onHide={() => {if (!visibleFriendDialog) return; setVisibleFriendDialog(false); }}
                    footer={footerFriendContent}
            >
                <div className="card flex justify-content-around"
                     style={{width: '100%', padding: '20px'}}>
                    <FloatLabel style={{ margin: '5px'}}>
                        <InputText id="friend"
                                   value={friend}
                                   onChange={(e) => setFriend(e.target.value)}
                        />
                        <label htmlFor="mailTo">Кому</label>
                    </FloatLabel>
                </div>
            </Dialog>
            <Toast ref={toast}/>

            <ConfirmDialog group="declarative"
                           visible={visibleFriendCoinf}
                           onHide={() => setVisibleFriendCoinf(false)}
                           message={coinfMessage}
                           header="Подтверждение"
                           icon="pi pi-question-circle"
                           closable={false}
                           acceptLabel='Да'
                           rejectLabel='Нет'
                           accept={() => {
                               answerOnFriendRequest(true);
                           }}
                           reject={() => {
                               answerOnFriendRequest(false);
                           }} />
        </div>
    );
}